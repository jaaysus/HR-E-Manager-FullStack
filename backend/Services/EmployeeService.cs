using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace HrETracker.Services;

public class EmployeeService(HrETrackerDbContext db, IHttpContextAccessor context, TimeProvider clock, StockWorkflow workflow) : IEmployeeService
{
    public async Task<EmployeePage> GetAsync(string? query, Guid? departmentId, string? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("Page must be positive and pageSize must be between 1 and 100.");
        if (status is not (null or "active" or "inactive" or "all")) throw new ArgumentException("Status must be active, inactive, or all.");
        var employees = db.Employees.AsNoTracking().Include(e => e.Department).AsQueryable();
        if (status != "all") employees = employees.Where(e => e.IsActive == (status != "inactive"));
        if (departmentId.HasValue) employees = employees.Where(e => e.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var value = query.Trim();
            employees = employees.Where(e => e.EmployeeNumber.Contains(value) || e.FullName.Contains(value));
        }
        var count = await employees.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset > int.MaxValue ? [] : await employees.OrderBy(e => e.EmployeeNumber).ThenBy(e => e.Id)
            .Skip((int)offset).Take(pageSize).ToListAsync(ct);
        return new(items.Select(ToResponse).ToArray(), count, page, pageSize);
    }

    public async Task<EmployeeResponse?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var employee = await db.Employees.AsNoTracking().Include(e => e.Department).SingleOrDefaultAsync(e => e.Id == id, ct);
        return employee is null ? null : ToResponse(employee);
    }

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken ct)
    {
        Validate(request.FullName, request.EnrollmentDate);
        await using var tx = await workflow.Begin(ct);
        if (string.IsNullOrWhiteSpace(request.EmployeeNumber)) throw new ArgumentException("Employee number is required.");
        var number = request.EmployeeNumber.Trim().ToUpperInvariant();
        if (await db.Employees.AnyAsync(e => e.EmployeeNumber == number, ct)) throw new EmployeeConflictException("Employee number already exists, including inactive employees.");
        var employee = new Employee
        {
            EmployeeNumber = number, FullName = request.FullName.Trim(), DepartmentId = request.DepartmentId,
            Department = await GetDepartment(request.DepartmentId, ct), JobTitle = Clean(request.JobTitle),
            EnrollmentDate = request.EnrollmentDate, Notes = Clean(request.Notes),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime, CreatedByUserId = Actor
        };
        Stamp(employee);
        db.Employees.Add(employee);
        Audit(employee, "Employee.Created", null);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new EmployeeConflictException("Employee number already exists."); }
        await tx.CommitAsync(ct);
        return ToResponse(employee);
    }

    public async Task<EmployeeResponse?> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct)
    {
        Validate(request.FullName, request.EnrollmentDate);
        await using var tx = await workflow.Begin(ct);
        var employee = await db.Employees.Include(e => e.Department).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return null;
        CheckVersion(employee, request.RowVersion);
        var before = Snapshot(employee);
        if (request.EmployeeNumber is not null)
        {
            var number = request.EmployeeNumber.Trim().ToUpperInvariant();
            if (number.Length == 0) throw new ArgumentException("Employee number is required.");
            if (await db.Employees.AnyAsync(e => e.Id != id && e.EmployeeNumber == number, ct)) throw new EmployeeConflictException("Employee number already exists.");
            employee.EmployeeNumber = number;
        }
        employee.Department = await GetDepartment(request.DepartmentId, ct);
        employee.DepartmentId = request.DepartmentId;
        employee.FullName = request.FullName.Trim();
        employee.JobTitle = Clean(request.JobTitle);
        employee.EnrollmentDate = request.EnrollmentDate;
        employee.Notes = Clean(request.Notes);
        Stamp(employee);
        Audit(employee, "Employee.Updated", before);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new EmployeeConflictException("Employee number already exists."); }
        await tx.CommitAsync(ct);
        return ToResponse(employee);
    }

    public async Task<EmployeeResponse?> SetActiveAsync(Guid id, SetEmployeeActiveRequest request, CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var employee = await db.Employees.Include(e => e.Department).SingleOrDefaultAsync(e => e.Id == id, ct);
        if (employee is null) return null;
        CheckVersion(employee, request.RowVersion);
        if (employee.IsActive == request.IsActive) return ToResponse(employee);
        if (request.IsActive) throw new EmployeeConflictException("Rehired employees require a new record, a new employee number, and a new enrollment date.");
        var before = Snapshot(employee);
        employee.IsActive = false;
        Stamp(employee);
        Audit(employee, "Employee.Deactivated", before);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(employee);
    }

    private async Task<Department> GetDepartment(Guid id, CancellationToken ct) =>
        await db.Departments.SingleOrDefaultAsync(d => d.Id == id && d.IsActive, ct)
        ?? throw new ArgumentException("An active department is required.");
    private void Validate(string name, DateOnly date)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Full name is required.");
        if (date == default) throw new ArgumentException("Enrollment date is required.");
    }
    private void CheckVersion(Employee employee, string value)
    {
        byte[] version;
        try { version = Convert.FromBase64String(value); }
        catch (FormatException) { throw new ArgumentException("RowVersion must be a Base64 value."); }
        if (version.Length == 0) throw new ArgumentException("RowVersion is required.");
        if (!version.SequenceEqual(employee.RowVersion)) throw new DbUpdateConcurrencyException();
        db.Entry(employee).Property(e => e.RowVersion).OriginalValue = version;
    }
    private string? Actor => context.HttpContext?.User.FindFirstValue("sub");
    private void Stamp(Employee employee)
    {
        employee.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        employee.UpdatedByUserId = Actor;
    }
    // Names, notes, and job titles are deliberately excluded from audit snapshots.
    private static string Snapshot(Employee e) => JsonSerializer.Serialize(new { e.EmployeeNumber, e.DepartmentId, e.EnrollmentDate, e.IsActive });
    private void Audit(Employee employee, string action, string? before) => db.AuditEvents.Add(new AuditEvent
    {
        ActorUserId = Actor, EntityId = employee.Id, Action = action, BeforeJson = before,
        AfterJson = Snapshot(employee), OccurredAtUtc = clock.GetUtcNow().UtcDateTime,
        CorrelationId = context.HttpContext?.TraceIdentifier
    });
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static EmployeeResponse ToResponse(Employee e) => new(e.Id, e.EmployeeNumber, e.FullName, e.DepartmentId,
        e.Department.Name, e.JobTitle, e.EnrollmentDate, e.Notes, e.IsActive, Convert.ToBase64String(e.RowVersion));
}

public sealed class EmployeeConflictException(string message) : Exception(message);
