using HrETracker.Models;
namespace HrETracker.Services;
public interface IEmployeeService
{
    Task<EmployeePage> GetAsync(string? query, Guid? departmentId, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<EmployeeResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeResponse?> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeResponse?> SetActiveAsync(Guid id, SetEmployeeActiveRequest request, CancellationToken cancellationToken);
}
