using HrETracker.Models;
using HrETracker.Services;
using HrETracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HrETracker.Controllers;
[ApiController, Authorize(Policy = "coats.read")]
[Route("api/coat-requests")]
public class CoatRequestsController(IRequestCycleService service, HrETrackerDbContext db) : ControllerBase
{
    [HttpGet("active-count")]
    public async Task<IActionResult> ActiveCount(CancellationToken ct) => Ok(new
    {
        activeCount = await db.CoatRequests.CountAsync(r => r.Employee.IsActive &&
            (r.Status == CoatRequestStatus.Pending || r.Status == CoatRequestStatus.OutOfStock), ct)
    });
    [HttpGet]
    public async Task<ActionResult<RequestPage>> Get(CancellationToken ct, CoatRequestStatus? status = null, Guid? departmentId = null, int page = 1, int pageSize = 25) => Ok(await service.GetAsync(status, departmentId, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Result(await service.GetByIdAsync(id, ct));
    [HttpPost("run-due-cycle"), Authorize(Policy = "coats.manage")]
    public async Task<IActionResult> Run(CancellationToken ct) => Ok(new { created = await service.CreateDueRequestsAsync(ct) });
    [HttpPost("{id:guid}/provide"), Authorize(Policy = "coats.provide")]
    public async Task<IActionResult> Provide(Guid id, RequestMutation input, CancellationToken ct)
    {
        await service.CreateDueRequestsAsync(ct);
        return Result(await service.ProvideAsync(id, input, ct));
    }
    [HttpPost("{id:guid}/cancel"), Authorize(Policy = "coats.manage")]
    public async Task<IActionResult> Cancel(Guid id, RequestMutation input, CancellationToken ct) => Result(await service.CancelAsync(id, input, ct));
    private IActionResult Result(CoatRequestResponse? result) => result is null ? Problem(statusCode: 404, title: "Coat request not found.") : Ok(result);
}
