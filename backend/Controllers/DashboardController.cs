using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HrETracker.Controllers;

[ApiController, Authorize(Policy = "coats.dashboard.read"), Route("api/dashboard")]
public class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken ct, int? year = null) => Ok(await service.Get(year, ct));
}
