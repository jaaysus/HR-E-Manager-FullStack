using System.Security.Claims;
using HrETracker.Data;
using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Controllers;

[ApiController, Route("api/program"), Authorize(Policy = "coats.dashboard.read")]
public class ProgramController(HrETrackerDbContext db, IRequestCycleService cycles, TimeProvider clock, StockWorkflow workflow, OrganizationService organization) : ControllerBase
{
    // POST explicitly reconciles current-cycle requests before returning the demo's shared view.
    // All program readers may trigger this idempotent scheduled operation, but cannot issue stock.
    [HttpPost("snapshot")]
    public async Task<IActionResult> Snapshot(CancellationToken ct)
    {
        await cycles.CreateDueRequestsAsync(ct);
        await using var tx = await workflow.Begin(ct);
        var settings = await db.OrganizationSettings.AsNoTracking().SingleAsync(ct);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
        var actor = User.FindFirstValue("sub")!;
        var employees = await db.Employees.AsNoTracking().Include(e => e.Department).Where(e => e.IsActive).OrderBy(e => e.EmployeeNumber).ToListAsync(ct);
        var requests = await db.CoatRequests.AsNoTracking().Include(r => r.Employee).ToListAsync(ct);
        var items = await db.InventoryItems.AsNoTracking().Include(i => i.Balance).Where(i => i.IsActive).ToListAsync(ct);
        var notifications = await organization.NotificationFeed().OrderByDescending(n => n.CreatedAtUtc).ThenBy(n => n.Id).ToListAsync(ct);
        var reads = (await db.NotificationReads.Where(r => r.UserId == actor).Select(r => r.NotificationId).ToListAsync(ct)).ToHashSet();
        var movements = await db.InventoryMovements.AsNoTracking().Include(m => m.InventoryItem).OrderByDescending(m => m.OccurredAtUtc).ThenBy(m => m.Id).ToListAsync(ct);
        var requestRecipients = requests.ToDictionary(r => r.Id, r => r.Employee);
        var receiverIds = movements.Where(m => m.Type == InventoryMovementType.Receipt && m.ActorUserId != null).Select(m => m.ActorUserId!).Distinct().ToArray();
        var restockReceivers = await db.Users.AsNoTracking().Where(u => receiverIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace(u.FullName) ? u.UserName : u.FullName, ct);
        var result = new
        {
            businessDate = today,
            employees = employees.Select(e =>
            {
                var cycle = RequestCycleService.PolicyCycle(e, requests, settings, clock.GetUtcNow()).Cycle;
                var current = requests.SingleOrDefault(r => r.EmployeeId == e.Id && r.CycleNumber == cycle);
                var completed = requests.Any(r => r.EmployeeId == e.Id && r.Status == CoatRequestStatus.Provided && r.CycleNumber >= cycle);
                return new
                {
                    e.Id, e.EmployeeNumber, e.FullName, e.DepartmentId, Department = e.Department.Name,
                    e.JobTitle, e.EnrollmentDate, e.Notes, e.IsActive, RowVersion = Convert.ToBase64String(e.RowVersion),
                    currentCycle = cycle, monthsEnrolled = Math.Max(0, (today.Year - e.EnrollmentDate.Year) * 12 + today.Month - e.EnrollmentDate.Month),
                    nextEligibilityAtUtc = RequestCycleService.NextEligibilityAtUtc(e, requests, settings, clock.GetUtcNow()),
                    coatStatus = cycle == 0 ? "none" : completed ? "provided" : current?.Status == CoatRequestStatus.Cancelled ? "cancelled" : current?.Status == CoatRequestStatus.Pending ? "requested" : "out_of_stock",
                    requestId = current?.Id, requestRowVersion = current is null ? null : Convert.ToBase64String(current.RowVersion),
                    requiredCoatId = items.SingleOrDefault(i => i.Id == current?.InventoryItemId)?.Sku
                };
            }).ToArray(),
            items = items.Select(i => new { i.Id, i.Sku, i.Name, i.Color, i.Size, quantityOnHand = i.Balance?.QuantityOnHand ?? 0, rowVersion = Convert.ToBase64String(i.Balance?.RowVersion ?? []) }).ToArray(),
            movements = movements.Select(m =>
            {
                var employee = m.Type == InventoryMovementType.Allocation && m.CoatRequestId.HasValue
                    ? requestRecipients.GetValueOrDefault(m.CoatRequestId.Value) : null;
                var recipientName = employee?.FullName ?? (m.Type == InventoryMovementType.Receipt && m.ActorUserId != null
                    ? restockReceivers.GetValueOrDefault(m.ActorUserId) : null);
                return new { m.Id, date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(m.OccurredAtUtc, DateTimeKind.Utc), zone)), type = m.Type == InventoryMovementType.Receipt ? "arrival" : m.Type == InventoryMovementType.Allocation ? "allocation" : "adjustment", qty = m.Quantity, coatType = m.InventoryItem.Sku, itemName = m.InventoryItem.Name, color = m.InventoryItem.Color, size = m.InventoryItem.Size, m.Note, recipientName, recipientEmployeeNumber = employee?.EmployeeNumber };
            }).ToArray(),
            notifications = notifications.Select(n => new { n.Id, n.Message, eventType = n.Type, n.RelatedEntityType, date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc), zone)), read = reads.Contains(n.Id), type = n.Type is "LowStock" or "ConfigurationRequired" ? "warning" : n.Type is "RequestProvided" or "StockArrival" ? "success" : "info" }).ToArray(),
            settings = new SettingsResponse(settings.TimeZoneId, settings.LowStockAlertsEnabled, settings.LowStockThreshold, Convert.ToBase64String(settings.RowVersion), settings.CompanyName)
        };
        await tx.CommitAsync(ct);
        return Ok(result);
    }
}
