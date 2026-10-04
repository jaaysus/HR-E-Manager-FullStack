namespace HrETracker.Models;

public enum CoatRequestStatus { Pending, OutOfStock, Provided, Cancelled }

public class CoatRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid? InventoryItemId { get; set; }
    public int CycleNumber { get; set; }
    public DateOnly DueDate { get; set; }
    public CoatRequestStatus Status { get; set; }
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProvidedAtUtc { get; set; }
    public string? ProvidedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? Notes { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Employee Employee { get; set; } = null!;
    public InventoryItem? InventoryItem { get; set; } = null!;
}

public record CoatRequestResponse(Guid Id, string EmployeeNumber, string EmployeeName, int CycleNumber,
    DateOnly DueDate, string? ItemSku, string? ItemName, CoatRequestStatus Status, DateTime? ProvidedAtUtc, string RowVersion, Guid? InventoryItemId, string? Notes);
