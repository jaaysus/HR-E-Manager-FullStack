using HrETracker.Models;
namespace HrETracker.Services;
public interface IInventoryService
{
    Task<InventoryItemResponse?> SaveVariantAsync(Guid? id, CoatVariantInput input, CancellationToken ct);
    Task<bool> DeleteVariantAsync(Guid id, string rowVersion, CancellationToken ct);
    Task<IReadOnlyList<InventoryItemResponse>> GetItemsAsync(CancellationToken ct);
    Task<InventoryItemResponse?> RecordReceiptAsync(RecordReceiptRequest request, CancellationToken ct);
    Task<InventoryItemResponse?> RecordAdjustmentAsync(RecordAdjustmentRequest request, CancellationToken ct);
    Task<object?> GetMovementsAsync(Guid id, int page, int pageSize, CancellationToken ct);
}
