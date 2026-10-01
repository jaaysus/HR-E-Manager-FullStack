using System.ComponentModel.DataAnnotations;

namespace HrETracker.Models;

public record EmployeeResponse(Guid Id, string EmployeeNumber, string FullName, string Department,
    string? JobTitle, DateOnly EnrollmentDate, string? Notes, bool IsActive, string RowVersion);

public class CreateEmployeeRequest
{
    [Required, StringLength(50)] public string EmployeeNumber { get; init; } = string.Empty;
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, StringLength(100)] public string Department { get; init; } = string.Empty;
    [StringLength(150)] public string? JobTitle { get; init; }
    public DateOnly EnrollmentDate { get; init; }
    [StringLength(2000)] public string? Notes { get; init; }
}

public sealed class UpdateEmployeeRequest : CreateEmployeeRequest
{
    [Required] public string RowVersion { get; init; } = string.Empty;
}
