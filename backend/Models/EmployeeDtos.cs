using System.ComponentModel.DataAnnotations;

namespace HrETracker.Models;

public record EmployeeResponse(Guid Id, string EmployeeNumber, string FullName, Guid DepartmentId, string Department,
    string? JobTitle, DateOnly EnrollmentDate, string? Notes, bool IsActive, string RowVersion);

public class CreateEmployeeRequest
{
    [Required, StringLength(50)] public string EmployeeNumber { get; init; } = string.Empty;
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    public Guid DepartmentId { get; init; }
    [StringLength(150)] public string? JobTitle { get; init; }
    public DateOnly EnrollmentDate { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed class UpdateEmployeeRequest
{
    [StringLength(50)] public string? EmployeeNumber { get; init; }
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    public Guid DepartmentId { get; init; }
    [StringLength(150)] public string? JobTitle { get; init; }
    public DateOnly EnrollmentDate { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public sealed class SetEmployeeActiveRequest
{
    public bool IsActive { get; init; }
    [Required] public string RowVersion { get; init; } = string.Empty;
}

public record EmployeePage(IReadOnlyList<EmployeeResponse> Items, int TotalCount, int Page, int PageSize);
