using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HrETracker.Controllers;
[ApiController, Authorize, Route("api")]
public class ItemRulesController(ItemRuleService service) : ControllerBase
{
    [HttpPost("coat-requests/{id:guid}/assign-item"), Authorize(Policy = "coats.manage")]
    public async Task<IActionResult> Assign(Guid id, AssignRequestItem input, CancellationToken ct) => await service.Assign(id, input, ct) ? NoContent() : Problem(statusCode: 404, title: "Request not found.");
}
