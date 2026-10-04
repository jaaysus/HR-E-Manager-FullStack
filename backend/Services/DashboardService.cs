using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Services;

public class DashboardService(HrETrackerDbContext db, TimeProvider clock, OrganizationService organization)
{
    public async Task<DashboardResponse> Get(int? year, CancellationToken ct)
    {
        if (year is < 1900 or > 9998) throw new ArgumentException("Report year must be between 1900 and 9998.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var settings = await db.OrganizationSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var reportYear = year ?? today.Year;
        var months = new List<MonthlyIssue>();
        // UTC ranges keep the indexed ledger query server-side and honor business-month boundaries.
        for (var month = 1; month <= 12; month++)
        {
            var local = new DateTime(reportYear, month, 1);
            var start = TimeZoneInfo.ConvertTimeToUtc(local, zone);
            var end = TimeZoneInfo.ConvertTimeToUtc(local.AddMonths(1), zone);
            var count = await db.InventoryMovements.CountAsync(m => m.Type == InventoryMovementType.Allocation
                && m.OccurredAtUtc >= start && m.OccurredAtUtc < end, ct);
            months.Add(new(reportYear, month, count));
        }
        var result = new DashboardResponse(today, settings.TimeZoneId, reportYear,
            await db.Employees.CountAsync(e => e.IsActive, ct), await db.Employees.CountAsync(e => !e.IsActive, ct),
            await db.CoatRequests.CountAsync(r => r.Status == CoatRequestStatus.Pending, ct),
            await db.CoatRequests.CountAsync(r => r.Status == CoatRequestStatus.OutOfStock, ct),
            await db.CoatRequests.CountAsync(r => r.Status == CoatRequestStatus.Provided, ct),
            await db.CoatRequests.CountAsync(r => r.Status == CoatRequestStatus.Cancelled, ct),
            await db.InventoryBalances.SumAsync(b => (long)b.QuantityOnHand, ct),
            await db.InventoryItems.CountAsync(i => i.IsActive && (i.Balance == null || i.Balance.QuantityOnHand <= settings.LowStockThreshold), ct),
            await organization.UnreadCount(ct), months);
        await tx.CommitAsync(ct);
        return result;
    }
}
