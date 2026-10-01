using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Services;

public class InventoryService(HrETrackerDbContext dbContext) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryItemResponse>> GetItemsAsync(CancellationToken cancellationToken) =>
        await dbContext.InventoryItems.AsNoTracking().OrderBy(item => item.Sku)
            .Select(item => new InventoryItemResponse(item.Id, item.Sku, item.Name, item.Department, item.Season,
                item.Balance == null ? 0 : item.Balance.QuantityOnHand))
            .ToListAsync(cancellationToken);

    public async Task<InventoryItemResponse?> RecordReceiptAsync(RecordReceiptRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(request.Quantity), "Quantity must be positive.");
        var item = await dbContext.InventoryItems.Include(item => item.Balance)
            .SingleOrDefaultAsync(item => item.Id == request.InventoryItemId && item.IsActive, cancellationToken);
        if (item is null) return null;

        var balance = item.Balance ?? new InventoryBalance { InventoryItemId = item.Id };
        if (item.Balance is null) dbContext.InventoryBalances.Add(balance);
        balance.QuantityOnHand += request.Quantity;
        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = item.Id, Type = InventoryMovementType.Receipt, Quantity = request.Quantity,
            Note = string.IsNullOrWhiteSpace(request.Note) ? "Stock receipt" : request.Note.Trim()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new InventoryItemResponse(item.Id, item.Sku, item.Name, item.Department, item.Season, balance.QuantityOnHand);
    }
}
