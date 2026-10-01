using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Services;

public class RequestCycleService(HrETrackerDbContext dbContext) : IRequestCycleService
{
    public async Task<int> CreateDueRequestsAsync(CancellationToken cancellationToken)
    {
        var today = GetBusinessDate();
        var employees = await dbContext.Employees.Where(employee => employee.IsActive).ToListAsync(cancellationToken);
        var existing = await dbContext.CoatRequests.Select(request => new { request.EmployeeId, request.CycleNumber })
            .ToListAsync(cancellationToken);
        var existingCycles = existing.Select(value => (value.EmployeeId, value.CycleNumber)).ToHashSet();
        var created = 0;

        foreach (var employee in employees)
        {
            var currentCycle = GetCurrentCycle(employee.EnrollmentDate, today);
            for (var cycle = 1; cycle <= currentCycle; cycle++)
            {
                if (existingCycles.Contains((employee.Id, cycle))) continue;
                var dueDate = employee.EnrollmentDate.AddMonths(cycle * 6);
                var item = await ResolveItemAsync(employee.Department, dueDate, cancellationToken);
                if (item is null) continue;
                var quantity = await dbContext.InventoryBalances.Where(balance => balance.InventoryItemId == item.Id)
                    .Select(balance => balance.QuantityOnHand).SingleOrDefaultAsync(cancellationToken);
                dbContext.CoatRequests.Add(new CoatRequest
                {
                    EmployeeId = employee.Id, InventoryItemId = item.Id, CycleNumber = cycle, DueDate = dueDate,
                    Status = quantity > 0 ? CoatRequestStatus.Pending : CoatRequestStatus.OutOfStock
                });
                created++;
            }
        }
        if (created > 0) await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task<IReadOnlyList<CoatRequestResponse>> GetAsync(CoatRequestStatus? status, CancellationToken cancellationToken)
    {
        var requests = dbContext.CoatRequests.AsNoTracking().Include(request => request.Employee).Include(request => request.InventoryItem).AsQueryable();
        if (status is not null) requests = requests.Where(request => request.Status == status);
        return await requests.OrderByDescending(request => request.DueDate).Select(request => ToResponse(request)).ToListAsync(cancellationToken);
    }

    public async Task<CoatRequestResponse?> ProvideAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var request = await dbContext.CoatRequests.Include(value => value.Employee).Include(value => value.InventoryItem)
            .SingleOrDefaultAsync(value => value.Id == requestId, cancellationToken);
        if (request is null) return null;
        if (request.Status is CoatRequestStatus.Provided or CoatRequestStatus.Cancelled)
            throw new InvalidOperationException("This request cannot be provided.");

        var balance = await dbContext.InventoryBalances.SingleOrDefaultAsync(value => value.InventoryItemId == request.InventoryItemId, cancellationToken);
        if (balance is null || balance.QuantityOnHand < 1)
        {
            request.Status = CoatRequestStatus.OutOfStock;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new InvalidOperationException("This coat variant is out of stock.");
        }
        balance.QuantityOnHand--;
        request.Status = CoatRequestStatus.Provided;
        request.ProvidedAtUtc = DateTime.UtcNow;
        dbContext.InventoryMovements.Add(new InventoryMovement
        {
            InventoryItemId = request.InventoryItemId, CoatRequestId = request.Id, Type = InventoryMovementType.Allocation,
            Quantity = -1, Note = $"Issued to {request.Employee.FullName}"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(request);
    }

    private async Task<InventoryItem?> ResolveItemAsync(string department, DateOnly date, CancellationToken cancellationToken)
    {
        var isSummer = date.Month is >= 4 and <= 9;
        var sku = department switch
        {
            "Warehouse" => "warehouse-hi-vis", "Operations" or "Logistics" => "logistics-summer",
            "Maintenance" => isSummer ? "engineering-summer" : "engineering-winter",
            "Quality Control" => "quality-winter", _ => isSummer ? "production-summer" : "production-winter"
        };
        return await dbContext.InventoryItems.SingleOrDefaultAsync(item => item.Sku == sku, cancellationToken);
    }

    private static int GetCurrentCycle(DateOnly enrollmentDate, DateOnly today)
    {
        var months = (today.Year - enrollmentDate.Year) * 12 + today.Month - enrollmentDate.Month;
        if (today.Day < enrollmentDate.Day) months--;
        return Math.Max(0, months / 6);
    }

    private static DateOnly GetBusinessDate()
    {
        TimeZoneInfo timeZone;
        try { timeZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Casablanca"); }
        catch (TimeZoneNotFoundException) { timeZone = TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time"); }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
    }

    private static CoatRequestResponse ToResponse(CoatRequest request) => new(request.Id, request.Employee.EmployeeNumber,
        request.Employee.FullName, request.CycleNumber, request.DueDate, request.InventoryItem.Sku,
        request.InventoryItem.Name, request.Status, request.ProvidedAtUtc);
}
