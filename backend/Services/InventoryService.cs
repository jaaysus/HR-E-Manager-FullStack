using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
namespace HrETracker.Services;

public class InventoryService(HrETrackerDbContext db, StockWorkflow workflow, TimeProvider clock) : IInventoryService
{
    public async Task<InventoryItemResponse?> SaveVariantAsync(Guid? id, CoatVariantInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Color) || string.IsNullOrWhiteSpace(input.Size)) throw new ArgumentException("Name, color, and size are required.");
        await using var tx = await workflow.Begin(ct);
        var item = id.HasValue ? await db.InventoryItems.Include(i => i.Balance).SingleOrDefaultAsync(i => i.Id == id, ct) : new InventoryItem { Id = Guid.NewGuid(), Sku = "", Name = "", Department = "", Season = "" };
        if (item is null) return null;
        if (id.HasValue) StockWorkflow.CheckVersion(item.Balance!.RowVersion, input.RowVersion!);
        var name = input.Name.Trim(); var color = input.Color.Trim(); var size = input.Size.Trim();
        if (input.IsActive && await db.InventoryItems.AnyAsync(i => i.Id != item.Id && i.IsActive && i.Name == name && i.Color == color && i.Size == size, ct)) throw new WorkflowConflictException("A coat with this name, color, and size already exists.");
        if (!input.IsActive && item.Balance?.QuantityOnHand > 0) throw new WorkflowConflictException("Adjust remaining stock to zero before retiring a variant.");
        if (!id.HasValue) item.Sku = item.Id.ToString("N");
        item.Name = name; item.Color = color; item.Size = size; item.IsActive = input.IsActive;
        if (!id.HasValue)
        {
            item.Balance = new InventoryBalance { InventoryItemId = item.Id };
            db.InventoryItems.Add(item);
        }
        else db.Entry(item.Balance!).Property(b => b.QuantityOnHand).IsModified = true;
        workflow.Audit("InventoryItem", item.Id, id.HasValue ? "Variant.Updated" : "Variant.Created", new { item.Name, item.Color, item.Size, item.IsActive });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(item);
    }
    public async Task<bool> DeleteVariantAsync(Guid id, string rowVersion, CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var item = await db.InventoryItems.Include(i => i.Balance).SingleOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return false;
        StockWorkflow.CheckVersion(item.Balance!.RowVersion, rowVersion);
        if (item.Balance.QuantityOnHand != 0) throw new WorkflowConflictException("Adjust remaining stock to zero before deleting a variant.");
        // Retire used variants so allocations and the stock ledger keep their references.
        item.IsActive = false;
        db.Entry(item.Balance).Property(b => b.QuantityOnHand).IsModified = true;
        workflow.Audit("InventoryItem", id, "Variant.Deleted", new { item.Sku });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
    public async Task<IReadOnlyList<InventoryItemResponse>> GetItemsAsync(CancellationToken ct)
    {
        var items = await db.InventoryItems.AsNoTracking().Include(i => i.Balance).OrderBy(i => i.Sku).ToListAsync(ct);
        return items.Select(ToResponse).ToArray();
    }
    public Task<InventoryItemResponse?> RecordReceiptAsync(RecordReceiptRequest request, CancellationToken ct)
    {
        if (request.Quantity <= 0) throw new ArgumentException("Receipt quantity must be positive.");
        return Post(request.InventoryItemId, request.Quantity, InventoryMovementType.Receipt, request.Note ?? "Stock receipt", request.Reference, null, ct, request.ArrivalDate);
    }
    public Task<InventoryItemResponse?> RecordAdjustmentAsync(RecordAdjustmentRequest request, CancellationToken ct)
    {
        if (request.Quantity == 0 || string.IsNullOrWhiteSpace(request.Note)) throw new ArgumentException("A nonzero adjustment and reason are required.");
        return Post(request.InventoryItemId, request.Quantity, InventoryMovementType.Adjustment, request.Note, request.Reference, request.RowVersion, ct);
    }
    private async Task<InventoryItemResponse?> Post(Guid id, int quantity, InventoryMovementType type, string note, string? reference, string? version, CancellationToken ct, DateOnly? arrivalDate = null)
    {
        if (note.Length > 500 || reference?.Length > 100) throw new ArgumentException("Note or reference is too long.");
        await using var tx = await workflow.Begin(ct);
        var item = await db.InventoryItems.Include(i => i.Balance).SingleOrDefaultAsync(i => i.Id == id && i.IsActive, ct);
        if (item is null) return null;
        var balance = await workflow.Balance(id, ct);
        if (version is not null) StockWorkflow.CheckVersion(balance.RowVersion, version);
        var result = (long)balance.QuantityOnHand + quantity;
        if (result < 0 || result > int.MaxValue) throw new WorkflowConflictException("Adjustment would put stock outside the allowed range.");
        var affected = await db.InventoryBalances.Where(b => b.InventoryItemId == id && (long)b.QuantityOnHand + quantity >= 0 && (long)b.QuantityOnHand + quantity <= int.MaxValue)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.QuantityOnHand, b => b.QuantityOnHand + quantity), ct);
        if (affected != 1) throw new WorkflowConflictException("Stock changed. Refresh and retry.");
        var occurred = workflow.Now;
        if (arrivalDate.HasValue)
        {
            var settings = await db.OrganizationSettings.SingleAsync(ct);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
            if (arrivalDate.Value > today || arrivalDate.Value.Year < 1900) throw new ArgumentException("Arrival date must be between 1900 and today.");
            occurred = TimeZoneInfo.ConvertTimeToUtc(arrivalDate.Value.ToDateTime(new TimeOnly(12, 0)), zone);
        }
        db.InventoryMovements.Add(new InventoryMovement { InventoryItemId = id, Type = type, Quantity = quantity, Note = note.Trim(), Reference = reference?.Trim(), ActorUserId = workflow.Actor, OccurredAtUtc = occurred });
        if (type == InventoryMovementType.Receipt) workflow.Notify("StockArrival", $"New stock of {quantity} {item.Name}{Details(item)} arrived. {note.Trim()}", "InventoryItem", item.Id, $"stock-arrival:{Guid.NewGuid()}");
        workflow.Audit("InventoryItem", id, $"Inventory.{type}", new { Quantity = quantity, QuantityOnHand = result });
        await workflow.EvaluateStock(id, (int)result, ct);
        await db.SaveChangesAsync(ct);
        await db.Entry(balance).ReloadAsync(ct);
        item.Balance = balance;
        await tx.CommitAsync(ct);
        return ToResponse(item);
    }
    public async Task<object?> GetMovementsAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        if (!await db.InventoryItems.AnyAsync(i => i.Id == id, ct)) return null;
        var query = db.InventoryMovements.AsNoTracking().Where(m => m.InventoryItemId == id);
        return new { items = await query.OrderByDescending(m => m.OccurredAtUtc).ThenBy(m => m.Id).Skip(Offset(page, pageSize)).Take(pageSize)
            .Select(m => new { m.Id, m.Type, m.Quantity, m.OccurredAtUtc, m.CoatRequestId, m.Reference, m.Note, m.ActorUserId }).ToListAsync(ct), totalCount = await query.CountAsync(ct), page, pageSize };
    }
    internal static int Offset(int page, int size) => (int)Math.Min(((long)page - 1) * size, int.MaxValue);
    internal static string Details(InventoryItem i) => string.IsNullOrWhiteSpace(i.Color) && string.IsNullOrWhiteSpace(i.Size) ? "" : $" ({i.Color}, {i.Size})";
    private static InventoryItemResponse ToResponse(InventoryItem i) => new(i.Id, i.Sku, i.Name, i.Balance?.QuantityOnHand ?? 0, Convert.ToBase64String(i.Balance?.RowVersion ?? []), i.IsActive, i.Color, i.Size);
}
public sealed class WorkflowConflictException(string message) : Exception(message);
