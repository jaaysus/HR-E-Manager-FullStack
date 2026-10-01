using HrETracker.Models;

namespace HrETracker.Services;

public interface IInventoryService
{
    Task<IReadOnlyList<InventoryItemResponse>> GetItemsAsync(CancellationToken cancellationToken);
    Task<InventoryItemResponse?> RecordReceiptAsync(RecordReceiptRequest request, CancellationToken cancellationToken);
}
