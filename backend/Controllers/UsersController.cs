using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HrETracker.Controllers;

[ApiController, Authorize(Policy = "platform.users.manage")]
[Route("api/users")]
public class UsersController(IUserManagementService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> Get(
        [FromQuery, Range(1, 100000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default) => Ok(await users.GetAsync(page, pageSize, cancellationToken));

    [HttpGet("{id}")]
    public async Task<ActionResult<UserResponse>> GetById(string id, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await users.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
        }
        catch (UserManagementException exception) { return Problem(statusCode: exception.StatusCode, title: exception.Message); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserResponse>> Update(string id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await users.UpdateAsync(id, request, cancellationToken)); }
        catch (UserManagementException exception) { return Problem(statusCode: exception.StatusCode, title: exception.Message); }
    }
}
