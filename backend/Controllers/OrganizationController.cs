using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace HrETracker.Controllers;
[ApiController, Authorize, Route("api")]
public class OrganizationController(OrganizationService service) : ControllerBase
{
    [HttpGet("settings"), Authorize(Policy = "platform.settings.read")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await service.OrganizationSettings(ct));
    [HttpPut("settings"), Authorize(Policy = "platform.settings.manage")]
    public async Task<IActionResult> Update(OrganizationPreferencesInput input, CancellationToken ct) => Ok(await service.UpdateOrganization(input, ct));
    [HttpGet("clothing/settings"), Authorize(Policy = "coats.read")]
    public async Task<IActionResult> Clothing(CancellationToken ct) => Ok(await service.ClothingSettings(ct));
    [HttpPut("clothing/settings"), Authorize(Policy = "clothing.settings.manage")]
    public async Task<IActionResult> UpdateClothing(ClothingSettingsInput input, CancellationToken ct) => Ok(await service.UpdateClothing(input, ct));
    [HttpGet("notifications"), Authorize(Policy = "platform.notifications.read")]
    public async Task<IActionResult> Notifications(CancellationToken ct, int page = 1, int pageSize = 25) => Ok(await service.Notifications(page, pageSize, ct));
    [HttpGet("notifications/unread-count"), Authorize(Policy = "platform.notifications.read")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct) => Ok(new { unreadCount = await service.UnreadCount(ct) });
    [HttpPost("notifications/{id:guid}/read"), Authorize(Policy = "platform.notifications.read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct) => await service.Read(id, ct) ? NoContent() : Problem(statusCode: 404, title: "Notification not found.");
    [HttpPost("notifications/read-all"), Authorize(Policy = "platform.notifications.read")]
    public async Task<IActionResult> ReadAll(CancellationToken ct) { await service.Read(null, ct); return NoContent(); }
}
