namespace HrETracker.Models;

public enum InventoryMovementType { Receipt, Allocation, Adjustment }

public class InventoryItem
{
    public Guid Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string Color { get; set; } = "";
    public string Size { get; set; } = "";
    public required string Department { get; set; }
    public required string Season { get; set; }
    public bool IsActive { get; set; } = true;
    public InventoryBalance? Balance { get; set; }
}

public class InventoryBalance
{
    public Guid InventoryItemId { get; set; }
    public int QuantityOnHand { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public InventoryItem InventoryItem { get; set; } = null!;
}

public class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryItemId { get; set; }
    public Guid? CoatRequestId { get; set; }
    public InventoryMovementType Type { get; set; }
    public int Quantity { get; set; }
    public string? Reference { get; set; }
    public string? ActorUserId { get; set; }
    public required string Note { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public InventoryItem InventoryItem { get; set; } = null!;
}

public record InventoryItemResponse(Guid Id, string Sku, string Name, int QuantityOnHand, string RowVersion, bool IsActive = true, string Color = "", string Size = "");
public class RecordReceiptRequest
{
    public Guid InventoryItemId { get; init; }
    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int Quantity { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Note { get; init; }
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? Reference { get; init; }
    public DateOnly? ArrivalDate { get; init; }
}
