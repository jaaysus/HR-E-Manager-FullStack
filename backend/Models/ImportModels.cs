namespace HrETracker.Models;

public class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FileName { get; set; }
    public required string Sha256 { get; set; }
    public long FileSize { get; set; }
    public string? UploadedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CommittedAtUtc { get; set; }
    public string? CommittedByUserId { get; set; }
    public string Status { get; set; } = "Validated";
    public int ErrorCount { get; set; }
    public int ImportedCount { get; set; }
    public List<ImportRow> Rows { get; set; } = [];
}

public class ImportRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportBatchId { get; set; }
    public int RowNumber { get; set; }
    public string EmployeeNumber { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Department { get; set; } = "";
    public Guid? DepartmentId { get; set; }
    public DateOnly? EnrollmentDate { get; set; }
    public string? JobTitle { get; set; }
    public string? Notes { get; set; }
    public string ErrorsJson { get; set; } = "[]";
    public Guid? EmployeeId { get; set; }
}

public record ImportSummary(Guid Id, string FileName, string Sha256, long FileSize, string Status,
    int RowCount, int ErrorCount, int ImportedCount, DateTime CreatedAtUtc, string? UploadedByUserId,
    DateTime? CommittedAtUtc, string? CommittedByUserId);
public record ImportRowResponse(int RowNumber, string EmployeeNumber, string FullName, string Department,
    Guid? DepartmentId, DateOnly? EnrollmentDate, string? JobTitle, string? Notes, string[] Errors, Guid? EmployeeId);
public record ImportPreview(ImportSummary Batch, IReadOnlyList<ImportRowResponse> Rows, int Page, int PageSize);
public record ImportHistory(IReadOnlyList<ImportSummary> Items, int TotalCount, int Page, int PageSize);
