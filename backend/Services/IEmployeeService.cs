using HrETracker.Models;

namespace HrETracker.Services;

public interface IEmployeeService
{
    Task<IReadOnlyList<EmployeeResponse>> GetAsync(string? query, string? department, CancellationToken cancellationToken);
    Task<EmployeeResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken);
    Task<EmployeeResponse?> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken);
}
