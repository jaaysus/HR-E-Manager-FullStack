using HrETracker.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace HrETracker.Services;

public interface IAuthService
{
    Task<AuthenticatedUserResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthenticatedUserResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task<bool> ValidateSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
    Task LogoutAsync(Guid sessionId, string userId, CancellationToken cancellationToken);
    Task LogoutByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task<UserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken);
    Task<IdentityResult> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken);
}
