namespace HrETracker.Models;

public class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string EmployeeNumber { get; set; }
    public required string FullName { get; set; }
    public required string Department { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly EnrollmentDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
