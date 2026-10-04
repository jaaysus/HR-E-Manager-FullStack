using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
namespace HrETracker.Services;
public class ItemRuleService(HrETrackerDbContext db, StockWorkflow workflow)
{
    public async Task<bool> Assign(Guid id, AssignRequestItem input, CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var r = await db.CoatRequests.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (r is null) return false;
        StockWorkflow.CheckVersion(r.RowVersion, input.RowVersion);
        if (r.InventoryItemId != null || r.Status is CoatRequestStatus.Cancelled or CoatRequestStatus.Provided) throw new WorkflowConflictException("Only unassigned outstanding requests can be configured.");
        if (!await db.InventoryItems.AnyAsync(i => i.Id == input.InventoryItemId && i.IsActive, ct)) throw new ArgumentException("An active inventory item is required.");
        r.InventoryItemId = input.InventoryItemId;
        r.Status = await db.InventoryBalances.AnyAsync(b => b.InventoryItemId == input.InventoryItemId && b.QuantityOnHand > 0, ct) ? CoatRequestStatus.Pending : CoatRequestStatus.OutOfStock;
        r.Notes = input.Notes;
        r.UpdatedAtUtc = workflow.Now;
        r.UpdatedByUserId = workflow.Actor;
        var alert = await db.Notifications.SingleOrDefaultAsync(n => n.DeduplicationKey == $"request-due:{r.Id}", ct);
        if (alert?.Type == "ConfigurationRequired") alert.ResolvedAtUtc = workflow.Now;
        workflow.Audit("CoatRequest", r.Id, "CoatRequest.ItemAssigned", new { r.InventoryItemId, r.Status });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
