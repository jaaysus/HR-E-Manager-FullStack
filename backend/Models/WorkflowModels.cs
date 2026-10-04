using System.ComponentModel.DataAnnotations;
namespace HrETracker.Models;

public class DepartmentItemRule
{
    public DateTime CreatedAtUtc { get; set; } = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public string? CreatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public string? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = new byte[] { 1 };
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DepartmentId { get; set; }
    public string Season { get; set; } = "AllSeason";
    public Guid InventoryItemId { get; set; }
    public DateOnly EffectiveFrom { get; set; } = new(2024, 1, 1);
    public DateOnly? EffectiveTo { get; set; }
}
public class OrganizationSettings
{
    public int Id { get; set; } = 1;
    public string TimeZoneId { get; set; } = "Africa/Casablanca";
    public bool LowStockAlertsEnabled { get; set; } = true;
    public int LowStockThreshold { get; set; } = 15;
    public int EligibilityInterval { get; set; } = 6;
    public string EligibilityUnit { get; set; } = "Months";
    public DateTime? EligibilityEffectiveAtUtc { get; set; }
    public string CompanyName { get; set; } = "Lear Corporation";
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? RecipientUserId { get; set; }
    public required string Type { get; set; }
    public required string Message { get; set; }
    public required string RelatedEntityType { get; set; }
    public Guid RelatedEntityId { get; set; }
    public string? DeduplicationKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
public class RequestMutation
{
    public Guid? InventoryItemId { get; init; }
    [Required] public string RowVersion { get; init; } = "";
    [MaxLength(1000)] public string? Notes { get; init; }
}
public class RecordAdjustmentRequest
{
    public Guid InventoryItemId { get; init; }
    public int Quantity { get; init; }
    [Required, MaxLength(500)] public string Note { get; init; } = "";
    [Required] public string RowVersion { get; init; } = "";
    [MaxLength(100)] public string? Reference { get; init; }
}
public record RequestPage(IReadOnlyList<CoatRequestResponse> Items, int TotalCount, int Page, int PageSize);
public record SettingsResponse(string TimeZoneId, bool LowStockAlertsEnabled, int LowStockThreshold, string RowVersion, string CompanyName = "Lear Corporation");
public record OrganizationPreferencesResponse(string CompanyName, string TimeZoneId, string RowVersion, DateOnly? BusinessDate = null);
public record ClothingSettingsResponse(bool LowStockAlertsEnabled, int LowStockThreshold, string RowVersion, int EligibilityInterval = 6, string EligibilityUnit = "Months");
public class OrganizationPreferencesInput
{
    [Required, StringLength(200)] public string CompanyName { get; init; } = "";
    [Required] public string TimeZoneId { get; init; } = "";
    [Required] public string RowVersion { get; init; } = "";
}
public class ClothingSettingsInput
{
    [Range(1, 10000)] public int EligibilityInterval { get; init; } = 6;
    public string EligibilityUnit { get; init; } = "Months";
    public bool LowStockAlertsEnabled { get; init; }
    [Range(0, int.MaxValue)] public int LowStockThreshold { get; init; }
    [Required] public string RowVersion { get; init; } = "";
}
public record PersonalNotificationPreferences(bool NotifyLowStock, bool NotifyClothingActivity);

public class NotificationRead
{
    public Guid NotificationId { get; set; }
    public required string UserId { get; set; }
    public DateTime ReadAtUtc { get; set; }
}

public class SaveItemRuleRequest
{
    public string? RowVersion { get; init; }
    public Guid DepartmentId { get; init; }
    [Required] public string Season { get; init; } = "AllSeason";
    public Guid InventoryItemId { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
}
public class AssignRequestItem : RequestMutation
{
    public new Guid InventoryItemId { get; init; }
}
