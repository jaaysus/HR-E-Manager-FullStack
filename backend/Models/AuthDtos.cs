using System.ComponentModel.DataAnnotations;

namespace HrETracker.Models;

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

public record AuthenticatedUserResponse(string AccessToken, DateTime ExpiresAtUtc, string Email, string FullName, IReadOnlyList<string> Roles);

public class JwtSettings
{
    public string Issuer { get; init; } = "HrETracker";
    public string Audience { get; init; } = "HrETracker.Frontend";
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 60;
}

public class InitialAdminSettings
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
