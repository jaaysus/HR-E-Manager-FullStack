namespace HrETracker.Models;

public class AuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string RefreshTokenHash { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
