using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HrETracker.Controllers;
[ApiController, Authorize(Policy = "inventory.read")]
[Route("api/inventory")]
public class InventoryController(IInventoryService service) : ControllerBase
{
    [HttpPost("items"), Authorize(Policy = "inventory.manage")]
    public async Task<IActionResult> CreateVariant(CoatVariantInput input, CancellationToken ct) => Ok(await service.SaveVariantAsync(null, input, ct));
    [HttpPut("items/{id:guid}"), Authorize(Policy = "inventory.manage")]
    public async Task<IActionResult> UpdateVariant(Guid id, CoatVariantInput input, CancellationToken ct)
    {
        var result = await service.SaveVariantAsync(id, input, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpDelete("items/{id:guid}"), Authorize(Policy = "inventory.manage")]
    public async Task<IActionResult> DeleteVariant(Guid id, [FromQuery] string rowVersion, CancellationToken ct) => await service.DeleteVariantAsync(id, rowVersion, ct) ? NoContent() : NotFound();
    [HttpGet("items")]
    public async Task<ActionResult<IReadOnlyList<InventoryItemResponse>>> Items(CancellationToken ct) => Ok(await service.GetItemsAsync(ct));
    [HttpGet("items/{id:guid}/movements")]
    public async Task<IActionResult> Movements(Guid id, CancellationToken ct, int page = 1, int pageSize = 25)
    {
        var result = await service.GetMovementsAsync(id, page, pageSize, ct);
        return result is null ? Problem(statusCode: 404, title: "Inventory item not found.") : Ok(result);
    }
    [HttpPost("receipts"), Authorize(Policy = "inventory.manage")]
    public async Task<IActionResult> Receipt(RecordReceiptRequest input, CancellationToken ct)
    {
        var result = await service.RecordReceiptAsync(input, ct);
        return result is null ? Problem(statusCode: 404, title: "Active inventory item not found.") : Ok(result);
    }
    [HttpPost("adjustments"), Authorize(Policy = "inventory.manage")]
    public async Task<IActionResult> Adjustment(RecordAdjustmentRequest input, CancellationToken ct)
    {
        var result = await service.RecordAdjustmentAsync(input, ct);
        return result is null ? Problem(statusCode: 404, title: "Active inventory item not found.") : Ok(result);
    }
}
