namespace HrETracker.Models;

public class Department
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public record DepartmentResponse(Guid Id, string Code, string Name, bool IsActive, string RowVersion = "");
public class SaveDepartmentRequest
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(50)] public string Code { get; init; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100)] public string Name { get; init; } = "";
    public bool IsActive { get; init; } = true;
    public string? RowVersion { get; init; }
}
