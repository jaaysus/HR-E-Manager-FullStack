using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Services;

public class EmployeeService(HrETrackerDbContext dbContext) : IEmployeeService
{
    public async Task<IReadOnlyList<EmployeeResponse>> GetAsync(string? query, string? department, CancellationToken cancellationToken)
    {
        var employees = dbContext.Employees.AsNoTracking().Where(employee => employee.IsActive);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var value = query.Trim();
            employees = employees.Where(employee => employee.EmployeeNumber.Contains(value) || employee.FullName.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(department))
            employees = employees.Where(employee => employee.Department == department.Trim());

        return await employees.OrderBy(employee => employee.EmployeeNumber)
            .Select(employee => ToResponse(employee)).ToListAsync(cancellationToken);
    }

    public async Task<EmployeeResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Employees.AsNoTracking().Where(employee => employee.Id == id)
            .Select(employee => ToResponse(employee)).SingleOrDefaultAsync(cancellationToken);

    public async Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = new Employee
        {
            EmployeeNumber = request.EmployeeNumber.Trim(), FullName = request.FullName.Trim(),
            Department = request.Department.Trim(), JobTitle = request.JobTitle?.Trim(),
            EnrollmentDate = request.EnrollmentDate, Notes = request.Notes?.Trim()
        };
        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(employee);
    }

    public async Task<EmployeeResponse?> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null) return null;
        try { dbContext.Entry(employee).Property(item => item.RowVersion).OriginalValue = Convert.FromBase64String(request.RowVersion); }
        catch (FormatException) { throw new ArgumentException("RowVersion must be a Base64 value."); }

        employee.FullName = request.FullName.Trim();
        employee.Department = request.Department.Trim();
        employee.JobTitle = request.JobTitle?.Trim();
        employee.EnrollmentDate = request.EnrollmentDate;
        employee.Notes = request.Notes?.Trim();
        employee.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(employee);
    }

    private static EmployeeResponse ToResponse(Employee employee) => new(employee.Id, employee.EmployeeNumber,
        employee.FullName, employee.Department, employee.JobTitle, employee.EnrollmentDate, employee.Notes,
        employee.IsActive, Convert.ToBase64String(employee.RowVersion));
}
