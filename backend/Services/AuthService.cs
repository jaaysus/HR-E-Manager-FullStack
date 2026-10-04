using HrETracker.Models;
using HrETracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace HrETracker.Services;

public class AuthService(UserManager<ApplicationUser> userManager, HrETrackerDbContext db,
    JwtSettings jwtSettings, TimeProvider timeProvider) : IAuthService
{
    public async Task<AuthenticatedUserResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user)) return null;
        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        if (!(await userManager.ResetAccessFailedCountAsync(user)).Succeeded) return null;
        var refreshToken = NewRefreshToken();
        var session = new AuthSession
        {
            UserId = user.Id,
            SecurityStamp = await userManager.GetSecurityStampAsync(user),
            RefreshTokenHash = Hash(refreshToken),
            CreatedAtUtc = UtcNow,
            ExpiresAtUtc = UtcNow.AddDays(jwtSettings.RefreshTokenDays)
        };
        db.AuthSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return await IssueTokensAsync(user, session, refreshToken);
    }

    public async Task<AuthenticatedUserResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var session = await db.AuthSessions.Include(session => session.User)
            .SingleOrDefaultAsync(session => session.RefreshTokenHash == hash, cancellationToken);
        if (session is null || !IsValid(session) || await userManager.IsLockedOutAsync(session.User)) return null;

        var nextToken = NewRefreshToken();
        session.RefreshTokenHash = Hash(nextToken);
        session.Version = Guid.NewGuid();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Only one concurrent refresh can rotate this session successfully.
            return null;
        }
        return await IssueTokensAsync(session.User, session, nextToken);
    }

    public async Task<bool> ValidateSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue("sid"), out var sessionId)) return false;
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var stamp = principal.FindFirstValue("security_stamp");
        var session = await db.AuthSessions.AsNoTracking().Include(session => session.User)
            .SingleOrDefaultAsync(session => session.Id == sessionId && session.UserId == userId, cancellationToken);
        return session is not null && IsValid(session) && session.SecurityStamp == stamp
            && !(session.User.LockoutEnd > timeProvider.GetUtcNow());
    }

    public Task LogoutAsync(Guid sessionId, string userId, CancellationToken cancellationToken) =>
        db.AuthSessions.Where(session => session.Id == sessionId && session.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, UtcNow)
                .SetProperty(session => session.Version, Guid.NewGuid()), cancellationToken);

    public Task LogoutByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        return db.AuthSessions.Where(session => session.RefreshTokenHash == hash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.RevokedAtUtc, UtcNow)
                .SetProperty(session => session.Version, Guid.NewGuid()), cancellationToken);
    }

    public async Task<UserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : new UserResponse(user.Id, user.Email ?? string.Empty, user.FullName,
            user.IsActive, (await userManager.GetRolesAsync(user)).ToArray());
    }

    public async Task<IdentityResult> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return IdentityResult.Failed(new IdentityError { Description = "User does not exist." });
        // Identity updates the security stamp, invalidating all of the user's sessions.
        return await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
    }

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    private bool IsValid(AuthSession session) => session.RevokedAtUtc is null && session.ExpiresAtUtc > UtcNow
        && session.User.IsActive && session.SecurityStamp == session.User.SecurityStamp;
    private static string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<AuthenticatedUserResponse> IssueTokensAsync(ApplicationUser user, AuthSession session, string refreshToken)
    {

        var roles = await userManager.GetRolesAsync(user);
        var now = UtcNow;
        var expiresAtUtc = now.AddMinutes(jwtSettings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("name", user.FullName),
            new("sid", session.Id.ToString()),
            new("security_stamp", session.SecurityStamp)
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtSettings.Issuer, jwtSettings.Audience, claims,
            notBefore: now, expires: expiresAtUtc, signingCredentials: credentials);

        return new AuthenticatedUserResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc,
            user.Email ?? string.Empty, user.FullName, roles.ToArray(), refreshToken, session.ExpiresAtUtc);
    }
}
