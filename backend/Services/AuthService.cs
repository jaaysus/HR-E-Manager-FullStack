using HrETracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HrETracker.Services;

public class AuthService(UserManager<ApplicationUser> userManager, JwtSettings jwtSettings) : IAuthService
{
    public async Task<AuthenticatedUserResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password)) return null;

        var roles = await userManager.GetRolesAsync(user);
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(jwtSettings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwtSettings.Issuer, jwtSettings.Audience, claims,
            expires: expiresAtUtc, signingCredentials: credentials);

        return new AuthenticatedUserResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc,
            user.Email ?? string.Empty, user.FullName, roles.ToArray());
    }
}
