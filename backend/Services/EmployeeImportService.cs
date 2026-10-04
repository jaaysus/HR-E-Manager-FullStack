using System.Security.Cryptography;
using System.Text.Json;
using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Services;

public class EmployeeImportService(HrETrackerDbContext db, StockWorkflow workflow)
{
    public async Task<ImportSummary> Stage(IFormFile file, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx" or ".xls")) throw new ArgumentException("Only CSV, XLSX, and XLS files are supported.");
        if (file.Length is <= 0 or > ImportFileParser.MaxBytes) throw new ArgumentException("Files must be nonempty and no larger than 10 MB.");
        using var content = new MemoryStream();
        await file.CopyToAsync(content, ct);
        content.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(content));
        content.Position = 0;
        var batch = new ImportBatch
        {
            FileName = Path.GetFileName(file.FileName.Replace('\\', '/')), Sha256 = hash, FileSize = file.Length,
            UploadedByUserId = workflow.Actor, CreatedAtUtc = workflow.Now, Rows = ImportFileParser.Parse(content, extension, ct)
        };
        if (batch.FileName.Length > 255) throw new ArgumentException("File name exceeds 255 characters.");
        await using var tx = await workflow.Begin(ct);
        await Validate(batch, ct);
        db.ImportBatches.Add(batch);
        workflow.Audit("ImportBatch", batch.Id, "EmployeeImport.Staged", new { batch.Sha256, RowCount = batch.Rows.Count, batch.ErrorCount, batch.Status });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Summary(batch, batch.Rows.Count);
    }

    private async Task Validate(ImportBatch batch, CancellationToken ct)
    {
        var departments = await db.Departments.AsNoTracking().Where(d => d.IsActive).ToListAsync(ct);
        var numbers = batch.Rows.Select(r => r.EmployeeNumber).Distinct().ToArray();
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in numbers.Chunk(500))
            existing.UnionWith(await db.Employees.Where(e => chunk.Contains(e.EmployeeNumber)).Select(e => e.EmployeeNumber).ToListAsync(ct));
        var duplicates = batch.Rows.GroupBy(r => r.EmployeeNumber, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in batch.Rows)
        {
            var errors = JsonSerializer.Deserialize<string[]>(row.ErrorsJson)!.Where(e => e == "Column count does not match the header.").ToList();
            if (row.EmployeeNumber.Length is < 1 or > 50) errors.Add("Employee ID is required and must be at most 50 characters.");
            if (row.FullName.Length is < 1 or > 200) errors.Add("Full Name is required and must be at most 200 characters.");
            if (row.JobTitle?.Length > 150) errors.Add("Job Title must be at most 150 characters.");
            if (row.Notes?.Length > 2000) errors.Add("Notes must be at most 2000 characters.");
            if (row.EnrollmentDate is null || row.EnrollmentDate == default(DateOnly)) errors.Add("Enrollment Date must be yyyy-MM-dd or an Excel date cell.");
            var matches = departments.Where(d => d.Name.Equals(row.Department, StringComparison.OrdinalIgnoreCase) || d.Code.Equals(row.Department, StringComparison.OrdinalIgnoreCase)).ToList();
            row.DepartmentId = matches.Count == 1 ? matches[0].Id : null;
            if (row.DepartmentId is null) errors.Add("Department must match an active department name or code.");
            if (duplicates.Contains(row.EmployeeNumber)) errors.Add("Employee ID is duplicated within this file.");
            if (existing.Contains(row.EmployeeNumber)) errors.Add("Employee ID already exists, including inactive employees.");
            row.ErrorsJson = JsonSerializer.Serialize(errors);
        }
        batch.ErrorCount = batch.Rows.Count(r => r.ErrorsJson != "[]");
        batch.Status = batch.ErrorCount == 0 ? "Validated" : "Invalid";
    }

    public async Task<ImportSummary?> Commit(Guid id, CancellationToken ct)
    {
        await using var tx = await workflow.Begin(ct);
        var batch = await db.ImportBatches.Include(b => b.Rows).SingleOrDefaultAsync(b => b.Id == id, ct);
        if (batch is null) return null;
        if (batch.Status == "Committed") return Summary(batch, batch.Rows.Count);
        if (batch.Status != "Validated") throw new WorkflowConflictException("Invalid batches cannot be committed. Correct the file and upload a new batch.");
        await Validate(batch, ct);
        if (batch.ErrorCount > 0)
        {
            workflow.Audit("ImportBatch", id, "EmployeeImport.CommitRejected", new { batch.ErrorCount });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw new WorkflowConflictException("Stored data changed after preview. Review the updated row errors and upload a corrected file.");
        }
        foreach (var row in batch.Rows.OrderBy(r => r.RowNumber))
        {
            // All rows were revalidated in this transaction. Save together to keep large imports bounded.
            var employee = new Employee
            {
                EmployeeNumber = row.EmployeeNumber, FullName = row.FullName, DepartmentId = row.DepartmentId!.Value,
                EnrollmentDate = row.EnrollmentDate!.Value, JobTitle = row.JobTitle, Notes = row.Notes,
                CreatedAtUtc = workflow.Now, UpdatedAtUtc = workflow.Now,
                CreatedByUserId = workflow.Actor, UpdatedByUserId = workflow.Actor
            };
            db.Employees.Add(employee);
            workflow.Audit("Employee", employee.Id, "Employee.Created", new { employee.EmployeeNumber, employee.DepartmentId, employee.EnrollmentDate, employee.IsActive });
            row.EmployeeId = employee.Id;
        }
        batch.Status = "Committed";
        batch.CommittedAtUtc = workflow.Now;
        batch.CommittedByUserId = workflow.Actor;
        batch.ImportedCount = batch.Rows.Count;
        workflow.Audit("ImportBatch", id, "EmployeeImport.Committed", new { batch.ImportedCount, batch.Sha256 });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Summary(batch, batch.Rows.Count);
    }

    public async Task<ImportPreview?> Preview(Guid id, int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize);
        var batch = await db.ImportBatches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, ct);
        if (batch is null) return null;
        var count = await db.ImportRows.CountAsync(r => r.ImportBatchId == id, ct);
        var rows = await db.ImportRows.AsNoTracking().Where(r => r.ImportBatchId == id).OrderBy(r => r.RowNumber)
            .Skip(InventoryService.Offset(page, pageSize)).Take(pageSize).ToListAsync(ct);
        return new(Summary(batch, count), rows.Select(r => new ImportRowResponse(r.RowNumber, r.EmployeeNumber, r.FullName,
            r.Department, r.DepartmentId, r.EnrollmentDate, r.JobTitle, r.Notes, JsonSerializer.Deserialize<string[]>(r.ErrorsJson)!, r.EmployeeId)).ToArray(), page, pageSize);
    }

    public async Task<ImportHistory> History(int page, int pageSize, CancellationToken ct)
    {
        Page(page, pageSize);
        var count = await db.ImportBatches.CountAsync(ct);
        var batches = await db.ImportBatches.AsNoTracking().OrderByDescending(b => b.CreatedAtUtc).ThenBy(b => b.Id)
            .Skip(InventoryService.Offset(page, pageSize)).Take(pageSize)
            .Select(b => new { Batch = b, Count = b.Rows.Count }).ToListAsync(ct);
        return new(batches.Select(b => Summary(b.Batch, b.Count)).ToArray(), count, page, pageSize);
    }
    private static void Page(int page, int size) { if (page < 1 || size is < 1 or > 100) throw new ArgumentException("Invalid pagination."); }
    private static ImportSummary Summary(ImportBatch b, int rows) => new(b.Id, b.FileName, b.Sha256, b.FileSize, b.Status,
        rows, b.ErrorCount, b.ImportedCount, b.CreatedAtUtc, b.UploadedByUserId, b.CommittedAtUtc, b.CommittedByUserId);
}
