using HrETracker.Models;

namespace HrETracker.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserResponse>> GetAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<UserResponse?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<UserResponse> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken);
}

public class UserManagementException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
