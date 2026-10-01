using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrETracker.Controllers;

[ApiController, Authorize(Roles = "HrAdministrator,InventoryManager,Viewer")]
[Route("api/inventory")]
public class InventoryController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemResponse>>> GetItems(CancellationToken cancellationToken) =>
        Ok(await inventoryService.GetItemsAsync(cancellationToken));

    [HttpPost("receipts"), Authorize(Roles = "HrAdministrator,InventoryManager")]
    public async Task<ActionResult<InventoryItemResponse>> RecordReceipt(RecordReceiptRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var item = await inventoryService.RecordReceiptAsync(request, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (ArgumentOutOfRangeException exception) { return BadRequest(new ProblemDetails { Title = exception.Message }); }
    }
}
