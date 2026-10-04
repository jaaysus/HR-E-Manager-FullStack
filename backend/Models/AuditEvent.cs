namespace HrETracker.Models;

public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? ActorUserId { get; set; }
    public required string Action { get; set; }
    public string EntityType { get; set; } = "Employee";
    public Guid EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
    public string? BeforeJson { get; set; }
    public required string AfterJson { get; set; }
}
