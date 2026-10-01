using HrETracker.Models;

namespace HrETracker.Services;

public interface IAuthService
{
    Task<AuthenticatedUserResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
