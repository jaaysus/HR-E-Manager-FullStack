using System.ComponentModel.DataAnnotations;

namespace HrETracker.Models;

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

public record AuthenticatedUserResponse(string AccessToken, DateTime ExpiresAtUtc, string Email,
    string FullName, IReadOnlyList<string> Roles, string RefreshToken, DateTime RefreshTokenExpiresAtUtc)
{
    public IReadOnlyList<string> Permissions => AccessPolicies.ForRoles(Roles);
}

public record BrowserSessionResponse(string AccessToken, DateTime ExpiresAtUtc, string Email,
    string FullName, IReadOnlyList<string> Roles, DateTime RefreshTokenExpiresAtUtc)
{
    public IReadOnlyList<string> Permissions => AccessPolicies.ForRoles(Roles);
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; init; } = string.Empty;
}

public record UserResponse(string Id, string Email, string FullName, bool IsActive, IReadOnlyList<string> Roles)
{
    public IReadOnlyList<string> Permissions => AccessPolicies.ForRoles(Roles);
}

public class CreateUserRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; init; } = string.Empty;
    [Required, MinLength(1), MaxLength(3)] public string[] Roles { get; init; } = [];
}

public class UpdateUserRequest
{
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    [Required, MinLength(1), MaxLength(3)] public string[] Roles { get; init; } = [];
}

public class JwtSettings
{
    public string Issuer { get; init; } = "HrETracker";
    public string Audience { get; init; } = "HrETracker.Frontend";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}

public class InitialAdminSettings
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FullName { get; init; } = "HR Administrator";
}
