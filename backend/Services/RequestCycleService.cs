using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
namespace HrETracker.Services;

public class RequestCycleService(HrETrackerDbContext db, StockWorkflow workflow, TimeProvider clock) : IRequestCycleService
{
    public async Task<int> CreateDueRequestsAsync(CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var settings = await db.OrganizationSettings.SingleAsync(ct);
        var employees = await db.Employees.AsNoTracking().Where(e => e.IsActive).ToListAsync(ct);
        var existing = await db.CoatRequests.ToListAsync(ct);
        var available = await db.InventoryItems.Where(i => i.IsActive && i.Balance != null && i.Balance.QuantityOnHand > 0).Select(i => i.Id).ToListAsync(ct);
        var created = 0;
        foreach (var e in employees)
        {
            var (cycles, due) = PolicyCycle(e, existing, settings, clock.GetUtcNow());
            // The demo exposes only the current cycle, never a backlog of missed issues.
            foreach (var previous in existing.Where(r => r.EmployeeId == e.Id && r.CycleNumber != cycles && r.Status is CoatRequestStatus.Pending or CoatRequestStatus.OutOfStock))
            {
                previous.Status = CoatRequestStatus.Cancelled;
                previous.UpdatedAtUtc = workflow.Now;
                previous.Notes = "Superseded by the current eligibility cycle.";
                workflow.Audit("CoatRequest", previous.Id, "CoatRequest.Superseded", new { previous.CycleNumber });
            }
            if (cycles > 0)
            {
                var cycle = cycles;
                var current = existing.SingleOrDefault(r => r.EmployeeId == e.Id && r.CycleNumber == cycle);
                Guid? itemId = current?.InventoryItemId;
                if (existing.Any(r => r.EmployeeId == e.Id && r.Status == CoatRequestStatus.Provided && r.CycleNumber >= cycle)) continue;
                if (current is not null)
                {
                    if (current.Status == CoatRequestStatus.Cancelled && current.Notes != "Superseded by the current eligibility cycle.") continue;
                    var status = itemId.HasValue && !available.Contains(itemId.Value) ? CoatRequestStatus.OutOfStock : CoatRequestStatus.Pending;
                    if (current.InventoryItemId != itemId || current.Status != status || current.DueDate != due)
                    {
                        current.InventoryItemId = itemId;
                        current.Status = status;
                        current.DueDate = due;
                        current.UpdatedAtUtc = workflow.Now;
                        workflow.Audit("CoatRequest", current.Id, "CoatRequest.Synchronized", new { cycle, itemId, status });
                    }
                    continue;
                }
                var request = new CoatRequest { EmployeeId = e.Id, CycleNumber = cycle, DueDate = due, InventoryItemId = itemId,
                    Status = CoatRequestStatus.Pending,
                    Notes = null,
                    RequestedAtUtc = settings.EligibilityEffectiveAtUtc is { } epoch && workflow.Now <= epoch ? epoch.AddTicks(1) : workflow.Now, UpdatedAtUtc = workflow.Now, CreatedByUserId = workflow.Actor, UpdatedByUserId = workflow.Actor };
                db.CoatRequests.Add(request);
                workflow.Notify("RequestDue", $"A clothing request is due for {e.FullName} ({e.EmployeeNumber}). Choose the coat variant when providing clothing.", "CoatRequest", request.Id, $"request-due:{request.Id}");
                workflow.Audit("CoatRequest", request.Id, "CoatRequest.Created", new { e.Id, cycle, due, itemId });
                created++;
            }
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return created;
    }
    // Match the demo's calendar-month eligibility calculation.
    public static int GetCurrentCycle(DateOnly enrollment, DateOnly today)
    {
        return Math.Max(0, ((today.Year - enrollment.Year) * 12 + today.Month - enrollment.Month) / 6);
    }
    public static (int Cycle, DateOnly Due) PolicyCycle(Employee employee, IEnumerable<CoatRequest> requests, OrganizationSettings settings, DateTimeOffset now)
    {
        var schedule = PolicySchedule(employee, requests, settings, now);
        return (schedule.Cycle, schedule.Due);
    }
    public static DateTime? NextEligibilityAtUtc(Employee employee, IEnumerable<CoatRequest> requests, OrganizationSettings settings, DateTimeOffset now) => PolicySchedule(employee, requests, settings, now).NextAtUtc;
    private static (int Cycle, DateOnly Due, DateTime? NextAtUtc) PolicySchedule(Employee employee, IEnumerable<CoatRequest> requests, OrganizationSettings settings, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(now, zone).DateTime;
        var anchor = employee.EnrollmentDate.ToDateTime(TimeOnly.MinValue);
        var offset = 0;
        if (settings.EligibilityEffectiveAtUtc is { } changed)
        {
            var effective = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(changed, DateTimeKind.Utc), zone);
            if (effective > anchor) anchor = effective;
            offset = requests.Where(r => r.EmployeeId == employee.Id && r.RequestedAtUtc <= changed).Select(r => r.CycleNumber).DefaultIfEmpty(0).Max();
        }
        int elapsed;
        DateTime due;
        DateTime? nextAtUtc;
        if (settings.EligibilityUnit == "Months")
        {
            elapsed = Math.Max(0, ((local.Year - anchor.Year) * 12 + local.Month - anchor.Month) / settings.EligibilityInterval);
            if (settings.EligibilityEffectiveAtUtc.HasValue && elapsed > 0 && anchor.AddMonths(elapsed * settings.EligibilityInterval) > local) elapsed--;
            due = anchor.AddMonths(elapsed * settings.EligibilityInterval);
            try { nextAtUtc = TimeZoneInfo.ConvertTimeToUtc(anchor.AddMonths((elapsed + 1) * settings.EligibilityInterval), zone); }
            catch (ArgumentOutOfRangeException) { nextAtUtc = null; }
        }
        else
        {
            var minutes = settings.EligibilityInterval * (settings.EligibilityUnit == "Days" ? 1440d : 1d);
            var anchorUtc = TimeZoneInfo.ConvertTimeToUtc(anchor, zone);
            elapsed = (int)Math.Max(0, Math.Floor((now.UtcDateTime - anchorUtc).TotalMinutes / minutes));
            due = TimeZoneInfo.ConvertTimeFromUtc(anchorUtc.AddMinutes(elapsed * minutes), zone);
            try { nextAtUtc = anchorUtc.AddMinutes((elapsed + 1) * minutes); }
            catch (ArgumentOutOfRangeException) { nextAtUtc = null; }
        }
        return (elapsed == 0 ? 0 : offset + elapsed, DateOnly.FromDateTime(due), nextAtUtc);
    }
    private IQueryable<CoatRequest> Query => db.CoatRequests.AsNoTracking().Include(r => r.Employee).Include(r => r.InventoryItem);
    public async Task<RequestPage> GetAsync(CoatRequestStatus? status, Guid? departmentId, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (status.HasValue && !Enum.IsDefined(status.Value))) throw new ArgumentException("Invalid status or pagination.");
        var query = Query;
        if (status.HasValue) query = query.Where(r => r.Status == status);
        if (departmentId.HasValue) query = query.Where(r => r.Employee.DepartmentId == departmentId);
        var total = await query.CountAsync(ct);
        var values = await query.OrderByDescending(r => r.DueDate).ThenBy(r => r.Id).Skip(InventoryService.Offset(page, pageSize)).Take(pageSize).ToListAsync(ct);
        return new(values.Select(ToResponse).ToArray(), total, page, pageSize);
    }
    public async Task<CoatRequestResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var r = await Query.SingleOrDefaultAsync(r => r.Id == id, ct);
        return r is null ? null : ToResponse(r);
    }
    public Task<CoatRequestResponse?> ProvideAsync(Guid id, RequestMutation input, CancellationToken ct) => Mutate(id, input, true, ct);
    public Task<CoatRequestResponse?> CancelAsync(Guid id, RequestMutation input, CancellationToken ct) => Mutate(id, input, false, ct);
    private async Task<CoatRequestResponse?> Mutate(Guid id, RequestMutation input, bool provide, CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var r = await db.CoatRequests.Include(r => r.Employee).Include(r => r.InventoryItem).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (r is null) return null;
        StockWorkflow.CheckVersion(r.RowVersion, input.RowVersion);
        if (r.Status is not (CoatRequestStatus.Pending or CoatRequestStatus.OutOfStock)) throw new WorkflowConflictException("This request is already provided or cancelled.");
        if (provide)
        {
            if (await db.CoatRequests.AnyAsync(other => other.EmployeeId == r.EmployeeId && other.Status == CoatRequestStatus.Provided && other.CycleNumber >= r.CycleNumber, ct)) throw new WorkflowConflictException("This employee's current cycle is already completed.");
            if (input.InventoryItemId.HasValue)
            {
                r.InventoryItemId = input.InventoryItemId;
                r.InventoryItem = await db.InventoryItems.SingleOrDefaultAsync(i => i.Id == input.InventoryItemId, ct);
            }
            if (!r.Employee.IsActive || r.InventoryItemId is null || r.InventoryItem?.IsActive != true) throw new WorkflowConflictException("An active employee and assigned active item are required.");
            var settings = await db.OrganizationSettings.SingleAsync(ct);
            var history = await db.CoatRequests.AsNoTracking().Where(x => x.EmployeeId == r.EmployeeId).ToListAsync(ct);
            if (r.CycleNumber != PolicyCycle(r.Employee, history, settings, clock.GetUtcNow()).Cycle) throw new WorkflowConflictException("This is not the employee's current request cycle. Refresh and retry.");
            var affected = await db.InventoryBalances.Where(b => b.InventoryItemId == r.InventoryItemId && b.QuantityOnHand >= 1)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.QuantityOnHand, b => b.QuantityOnHand - 1), ct);
            if (affected != 1) throw new WorkflowConflictException("This coat variant is out of stock.");
            r.Status = CoatRequestStatus.Provided;
            r.ProvidedAtUtc = workflow.Now;
            r.ProvidedByUserId = workflow.Actor;
            db.InventoryMovements.Add(new InventoryMovement { InventoryItemId = r.InventoryItemId.Value, CoatRequestId = r.Id, Type = InventoryMovementType.Allocation, Quantity = -1, Note = "Coat provided", ActorUserId = workflow.Actor, OccurredAtUtc = workflow.Now });
        }
        else r.Status = CoatRequestStatus.Cancelled;
        if (input.Notes?.Length > 1000) throw new ArgumentException("Notes must be at most 1000 characters.");
        r.Notes = input.Notes?.Trim() ?? r.Notes;
        r.UpdatedAtUtc = workflow.Now;
        r.UpdatedByUserId = workflow.Actor;
        var action = provide ? "Provided" : "Cancelled";
        workflow.Notify($"Request{action}", provide ? $"{r.InventoryItem!.Name}{InventoryService.Details(r.InventoryItem)} successfully assigned to {r.Employee.FullName} ({r.Employee.EmployeeNumber})." : $"Coat request cancelled for {r.Employee.FullName} ({r.Employee.EmployeeNumber}).", "CoatRequest", r.Id, $"request-{action}:{r.Id}");
        workflow.Audit("CoatRequest", r.Id, $"CoatRequest.{action}", new { r.Status, r.InventoryItemId, r.CycleNumber });
        // Save completion before refreshing outstanding requests for the changed balance.
        await db.SaveChangesAsync(ct);
        if (provide)
        {
            var quantity = await db.InventoryBalances.Where(b => b.InventoryItemId == r.InventoryItemId).Select(b => b.QuantityOnHand).SingleAsync(ct);
            await workflow.EvaluateStock(r.InventoryItemId!.Value, quantity, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return ToResponse(r);
    }
    private static CoatRequestResponse ToResponse(CoatRequest r) => new(r.Id, r.Employee.EmployeeNumber, r.Employee.FullName, r.CycleNumber, r.DueDate, r.InventoryItem?.Sku, r.InventoryItem?.Name, r.Status, r.ProvidedAtUtc, Convert.ToBase64String(r.RowVersion), r.InventoryItemId, r.Notes);
}
