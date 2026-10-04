using HrETracker.Data;
using HrETracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HrETracker.Services;

namespace HrETracker.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize(Policy = "platform.departments.read")]
public class DepartmentsController(HrETrackerDbContext db, StockWorkflow workflow) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentResponse>>> Get(CancellationToken ct) =>
        Ok(await db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentResponse(d.Id, d.Code, d.Name, d.IsActive, Convert.ToBase64String(d.RowVersion))).ToListAsync(ct));
    [HttpPost, Authorize(Policy = "platform.departments.manage")]
    public Task<IActionResult> Create(SaveDepartmentRequest input, CancellationToken ct) => Save(null, input, ct);
    [HttpPut("{id:guid}"), Authorize(Policy = "platform.departments.manage")]
    public Task<IActionResult> Update(Guid id, SaveDepartmentRequest input, CancellationToken ct) => Save(id, input, ct);
    private async Task<IActionResult> Save(Guid? id, SaveDepartmentRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) throw new ArgumentException("Department code and name are required.");
        await using var tx = await workflow.Begin(ct);
        var department = id.HasValue ? await db.Departments.SingleOrDefaultAsync(d => d.Id == id, ct) : new Department { Id = Guid.NewGuid(), Code = "", Name = "", CreatedAtUtc = workflow.Now, CreatedByUserId = workflow.Actor };
        if (department is null) return NotFound();
        if (id.HasValue) StockWorkflow.CheckVersion(department.RowVersion, input.RowVersion ?? "");
        var code = input.Code.Trim().ToUpperInvariant();
        var name = input.Name.Trim();
        if (await db.Departments.AnyAsync(d => d.Id != department.Id && (d.Code == code || d.Name == name), ct)) throw new WorkflowConflictException("Department code and name must be unique.");
        if (!input.IsActive && await db.Employees.AnyAsync(e => e.DepartmentId == department.Id && e.IsActive, ct)) throw new WorkflowConflictException("Move or deactivate active employees before deactivating their department.");
        department.Code = code;
        department.Name = name;
        department.IsActive = input.IsActive;
        department.UpdatedAtUtc = workflow.Now;
        department.UpdatedByUserId = workflow.Actor;
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite") department.RowVersion = Guid.NewGuid().ToByteArray();
        if (!id.HasValue) db.Departments.Add(department);
        workflow.Audit("Department", department.Id, id.HasValue ? "Department.Updated" : "Department.Created", new { code, name, department.IsActive });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new DepartmentResponse(department.Id, department.Code, department.Name, department.IsActive, Convert.ToBase64String(department.RowVersion)));
    }
}
