using System.Security.Claims;
using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
namespace HrETracker.Services;
public class OrganizationService(HrETrackerDbContext db, StockWorkflow workflow, IHttpContextAccessor http)
{
    private string Actor => http.HttpContext!.User.FindFirstValue("sub")!;
    private IQueryable<Notification> VisibleNotifications()
    {
        var clothingAccess = AccessPolicies.ForRoles(AccessPolicies.Roles.Where(http.HttpContext!.User.IsInRole)).Contains("coats.read");
        var preferences = db.Users.Where(u => u.Id == Actor);
        return db.Notifications.Where(n => (n.RecipientUserId == null || n.RecipientUserId == Actor)
            && (clothingAccess || (n.Type != "LowStock" && n.Type != "ConfigurationRequired" && n.Type != "RequestDue" && n.Type != "RequestProvided" && n.Type != "StockArrival"))
            && (n.Type != "LowStock" || preferences.Any(u => u.NotifyLowStock))
            && ((n.Type != "ConfigurationRequired" && n.Type != "RequestDue" && n.Type != "RequestProvided" && n.Type != "StockArrival") || preferences.Any(u => u.NotifyClothingActivity)));
    }
    public Task<int> UnreadCount(CancellationToken ct) => VisibleNotifications().AsNoTracking()
        .Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == Actor))
        .CountAsync(ct);
    public async Task<OrganizationPreferencesResponse> OrganizationSettings(CancellationToken ct)
    {
        var s = await db.OrganizationSettings.AsNoTracking().SingleAsync(ct);
        return OrganizationResponse(s);
    }
    public async Task<ClothingSettingsResponse> ClothingSettings(CancellationToken ct)
    {
        var s = await db.OrganizationSettings.AsNoTracking().SingleAsync(ct);
        return new(s.LowStockAlertsEnabled, s.LowStockThreshold, Convert.ToBase64String(s.RowVersion), s.EligibilityInterval, s.EligibilityUnit);
    }
    public async Task<OrganizationPreferencesResponse> UpdateOrganization(OrganizationPreferencesInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName)) throw new ArgumentException("Company name is required.");
        try { TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new ArgumentException("Unknown business time zone."); }
        await using var tx = await workflow.Begin(ct);
        var settings = await db.OrganizationSettings.SingleAsync(ct);
        StockWorkflow.CheckVersion(settings.RowVersion, input.RowVersion);
        settings.TimeZoneId = input.TimeZoneId;
        settings.CompanyName = input.CompanyName.Trim();
        settings.UpdatedAtUtc = workflow.Now;
        settings.UpdatedByUserId = workflow.Actor;
        await db.SaveChangesAsync(ct);
        workflow.Audit("OrganizationSettings", Guid.Empty, "OrganizationSettings.Updated", new { settings.TimeZoneId, settings.CompanyName });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return OrganizationResponse(settings);
    }
    public async Task<ClothingSettingsResponse> UpdateClothing(ClothingSettingsInput input, CancellationToken ct)
    {
        if (input.EligibilityInterval is < 1 or > 10000 || input.EligibilityUnit is not ("Minutes" or "Days" or "Months")) throw new ArgumentException("Choose an interval from 1 to 10000 minutes, days, or months.");
        if (input.LowStockThreshold < 0) throw new ArgumentException("Threshold must be nonnegative.");
        await using var tx = await workflow.Begin(ct);
        var settings = await db.OrganizationSettings.SingleAsync(ct);
        StockWorkflow.CheckVersion(settings.RowVersion, input.RowVersion);
        if (settings.EligibilityInterval != input.EligibilityInterval || settings.EligibilityUnit != input.EligibilityUnit)
        {
            settings.EligibilityEffectiveAtUtc = workflow.Now;
            settings.EligibilityInterval = input.EligibilityInterval;
            settings.EligibilityUnit = input.EligibilityUnit;
        }
        settings.LowStockThreshold = input.LowStockThreshold;
        settings.LowStockAlertsEnabled = input.LowStockAlertsEnabled;
        settings.UpdatedAtUtc = workflow.Now;
        settings.UpdatedByUserId = workflow.Actor;
        await db.SaveChangesAsync(ct);
        var items = await db.InventoryItems.Include(i => i.Balance).Where(i => i.IsActive).ToListAsync(ct);
        foreach (var item in items) await workflow.EvaluateStock(item.Id, item.Balance?.QuantityOnHand ?? 0, ct);
        workflow.Audit("ClothingSettings", Guid.Empty, "ClothingSettings.Updated", new { settings.LowStockThreshold, settings.LowStockAlertsEnabled, settings.EligibilityInterval, settings.EligibilityUnit });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(settings.LowStockAlertsEnabled, settings.LowStockThreshold, Convert.ToBase64String(settings.RowVersion), settings.EligibilityInterval, settings.EligibilityUnit);
    }
    public IQueryable<Notification> NotificationFeed() => VisibleNotifications().AsNoTracking();
    private OrganizationPreferencesResponse OrganizationResponse(OrganizationSettings settings) => new(settings.CompanyName, settings.TimeZoneId, Convert.ToBase64String(settings.RowVersion), DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(workflow.Now, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId))));
    public async Task<object> Notifications(int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid pagination.");
        var query = VisibleNotifications().AsNoTracking();
        return new { totalCount = await query.CountAsync(ct), page, pageSize, items = await query.OrderByDescending(n => n.CreatedAtUtc).ThenBy(n => n.Id).Skip(InventoryService.Offset(page, pageSize)).Take(pageSize)
            .Select(n => new { n.Id, n.Type, n.Message, n.RelatedEntityType, n.RelatedEntityId, n.CreatedAtUtc, n.ResolvedAtUtc,
                ReadAtUtc = db.NotificationReads.Where(r => r.NotificationId == n.Id && r.UserId == Actor).Select(r => (DateTime?)r.ReadAtUtc).FirstOrDefault() }).ToListAsync(ct) };
    }
    public async Task<bool> Read(Guid? id, CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var query = VisibleNotifications();
        if (id.HasValue && !await query.AnyAsync(n => n.Id == id, ct)) return false;
        if (id.HasValue) query = query.Where(n => n.Id == id);
        var missing = await query.Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == Actor)).Select(n => n.Id).ToListAsync(ct);
        foreach (var notificationId in missing) db.NotificationReads.Add(new NotificationRead { NotificationId = notificationId, UserId = Actor, ReadAtUtc = workflow.Now });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
