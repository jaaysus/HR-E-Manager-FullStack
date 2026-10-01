using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Controllers;

[ApiController]
[Route("api/v1/employees")]
[Authorize(Roles = "HrAdministrator,InventoryManager,Viewer")]
public class EmployeesController(IEmployeeService employeeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeResponse>>> Get([FromQuery] string? query, [FromQuery] string? department, CancellationToken cancellationToken) =>
        Ok(await employeeService.GetAsync(query, department, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [Authorize(Roles = "HrAdministrator")]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeeService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { employee.Id }, employee);
        }
        catch (DbUpdateException) { return Conflict(new ProblemDetails { Title = "Employee number already exists." }); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "HrAdministrator")]
    public async Task<ActionResult<EmployeeResponse>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeeService.UpdateAsync(id, request, cancellationToken);
            return employee is null ? NotFound() : Ok(employee);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = exception.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "The employee was changed by another user. Refresh and try again." }); }
        catch (DbUpdateException) { return Conflict(new ProblemDetails { Title = "Employee number already exists." }); }
    }
}
