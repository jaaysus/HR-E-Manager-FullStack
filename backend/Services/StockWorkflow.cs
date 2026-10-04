using System.Data;
using System.Security.Claims;
using System.Text.Json;
using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace HrETracker.Services;

// A transaction-owned SQL Server lock serializes these small organization-wide workflows,
// including alert deduplication and overlapping scheduled/manual cycle runs.
public class StockWorkflow(HrETrackerDbContext db, IHttpContextAccessor http, TimeProvider clock)
{
    public DateTime Now => clock.GetUtcNow().UtcDateTime;
    public string? Actor => http.HttpContext?.User.FindFirstValue("sub");
    public async Task<IDbContextTransaction> Begin(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'HrETracker.StockWorkflow', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @result < 0 THROW 51000, 'Stock workflow is busy; retry the operation.', 1;", ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    public static void CheckVersion(byte[] current, string value)
    {
        byte[] supplied;
        try { supplied = Convert.FromBase64String(value); }
        catch (Exception ex) when (ex is FormatException or ArgumentNullException) { throw new ArgumentException("RowVersion must be a Base64 value."); }
        if (supplied.Length == 0) throw new ArgumentException("RowVersion is required.");
        if (!current.SequenceEqual(supplied)) throw new DbUpdateConcurrencyException("The record changed. Refresh and retry.");
    }
    public void Audit(string type, Guid id, string action, object after) => db.AuditEvents.Add(new AuditEvent
    {
        ActorUserId = Actor, EntityType = type, EntityId = id, Action = action,
        OccurredAtUtc = Now, CorrelationId = http.HttpContext?.TraceIdentifier,
        AfterJson = JsonSerializer.Serialize(after)
    });
    public void Notify(string type, string message, string entity, Guid id, string key) => db.Notifications.Add(new Notification
    {
        Type = type, Message = message, RelatedEntityType = entity, RelatedEntityId = id,
        CreatedAtUtc = Now, DeduplicationKey = key
    });
    public async Task EvaluateStock(Guid itemId, int quantity, CancellationToken ct)
    {
        var settings = await db.OrganizationSettings.SingleAsync(ct);
        var key = $"low-stock:{itemId}";
        var alert = await db.Notifications.SingleOrDefaultAsync(n => n.DeduplicationKey == key, ct);
        if (settings.LowStockAlertsEnabled && quantity <= settings.LowStockThreshold)
        {
            if (alert is null)
            {
                var label = await db.InventoryItems.Where(i => i.Id == itemId).Select(i => i.Color == "" && i.Size == "" ? i.Name : i.Name + " (" + i.Color + ", " + i.Size + ")").SingleAsync(ct);
                Notify("LowStock", $"{label} has {quantity} units available.", "InventoryItem", itemId, key);
            }
        }
        else if (alert is not null)
        {
            alert.ResolvedAtUtc = Now;
            alert.DeduplicationKey = null;
        }
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var token = Guid.NewGuid().ToByteArray();
            await db.InventoryBalances.Where(b => b.InventoryItemId == itemId).ExecuteUpdateAsync(s => s.SetProperty(b => b.RowVersion, token), ct);
            await db.CoatRequests.Where(r => r.InventoryItemId == itemId && (r.Status == CoatRequestStatus.Pending || r.Status == CoatRequestStatus.OutOfStock))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.RowVersion, token), ct);
        }
        await db.CoatRequests.Where(r => r.InventoryItemId == itemId && (r.Status == CoatRequestStatus.Pending || r.Status == CoatRequestStatus.OutOfStock))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, quantity > 0 ? CoatRequestStatus.Pending : CoatRequestStatus.OutOfStock)
                .SetProperty(r => r.UpdatedAtUtc, Now).SetProperty(r => r.UpdatedByUserId, Actor), ct);
    }
    public async Task<InventoryBalance> Balance(Guid id, CancellationToken ct)
    {
        var balance = await db.InventoryBalances.SingleOrDefaultAsync(b => b.InventoryItemId == id, ct);
        if (balance is not null) return balance;
        balance = new InventoryBalance { InventoryItemId = id };
        db.InventoryBalances.Add(balance);
        await db.SaveChangesAsync(ct);
        return balance;
    }
}

