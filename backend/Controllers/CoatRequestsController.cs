using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrETracker.Controllers;

[ApiController, Authorize(Roles = "HrAdministrator,InventoryManager,Viewer")]
[Route("api/coat-requests")]
public class CoatRequestsController(IRequestCycleService requestCycleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CoatRequestResponse>>> Get([FromQuery] CoatRequestStatus? status, CancellationToken cancellationToken) =>
        Ok(await requestCycleService.GetAsync(status, cancellationToken));

    [HttpPost("run-due-cycle"), Authorize(Roles = "HrAdministrator")]
    public async Task<ActionResult<object>> RunDueCycle(CancellationToken cancellationToken) =>
        Ok(new { created = await requestCycleService.CreateDueRequestsAsync(cancellationToken) });

    [HttpPost("{id:guid}/provide"), Authorize(Roles = "HrAdministrator,InventoryManager")]
    public async Task<ActionResult<CoatRequestResponse>> Provide(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var request = await requestCycleService.ProvideAsync(id, cancellationToken);
            return request is null ? NotFound() : Ok(request);
        }
        catch (InvalidOperationException exception) { return Conflict(new ProblemDetails { Title = exception.Message }); }
    }
}
