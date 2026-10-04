using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HrETracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HrETracker.Controllers;

[ApiController, Route("api/auth/profile"), Authorize]
public class ProfileController(UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpPut]
    public async Task<IActionResult> Update(ProfileInput input)
    {
        var user = await users.FindByIdAsync(User.FindFirstValue("sub")!);
        if (user is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(input.FullName)) return Problem(statusCode: 400, title: "Full name is required.");
        user.FullName = input.FullName.Trim();
        if (input.Email is not null)
        {
            user.Email = input.Email.Trim();
            user.UserName = user.Email;
        }
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("fullName", error.Description);
            return ValidationProblem(ModelState);
        }
        return Ok(new { user.FullName, user.Email });
    }
    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue("sub")!);
        return user is null ? Unauthorized() : Ok(new PersonalNotificationPreferences(user.NotifyLowStock, user.NotifyClothingActivity));
    }
    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences(PersonalNotificationPreferences input)
    {
        var user = await users.FindByIdAsync(User.FindFirstValue("sub")!);
        if (user is null) return Unauthorized();
        user.NotifyLowStock = input.NotifyLowStock;
        user.NotifyClothingActivity = input.NotifyClothingActivity;
        var result = await users.UpdateAsync(user);
        return result.Succeeded ? Ok(input) : Problem(statusCode: 409, title: "Account changed. Refresh and retry.");
    }
}

public class ProfileInput
{
    [EmailAddress, StringLength(256)] public string? Email { get; init; }
    [Required, StringLength(200)] public string FullName { get; init; } = "";
}
