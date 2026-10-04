using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HrETracker.Controllers;

[ApiController, Authorize(Policy = "employees.import"), Route("api/employee-imports")]
public class EmployeeImportsController(EmployeeImportService service) : ControllerBase
{
    [HttpPost, Consumes("multipart/form-data"), RequestSizeLimit(11 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<ActionResult<ImportSummary>> Upload(IFormFile file, CancellationToken ct)
    {
        var batch = await service.Stage(file, ct);
        return CreatedAtAction(nameof(Preview), new { id = batch.Id }, batch);
    }
    [HttpGet]
    public async Task<ActionResult<ImportHistory>> History(CancellationToken ct, int page = 1, int pageSize = 25) => Ok(await service.History(page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportPreview>> Preview(Guid id, CancellationToken ct, int page = 1, int pageSize = 100)
    {
        var preview = await service.Preview(id, page, pageSize, ct);
        return preview is null ? Problem(statusCode: 404, title: "Import batch not found.") : Ok(preview);
    }
    [HttpPost("{id:guid}/commit")]
    public async Task<ActionResult<ImportSummary>> Commit(Guid id, CancellationToken ct)
    {
        var result = await service.Commit(id, ct);
        return result is null ? Problem(statusCode: 404, title: "Import batch not found.") : Ok(result);
    }
}
