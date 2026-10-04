using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = "employees.read")]
public class EmployeesController(IEmployeeService employeeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmployeePage>> Get([FromQuery] string? query, [FromQuery] Guid? departmentId,
        [FromQuery] string? status, CancellationToken cancellationToken, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
    {
        try { return Ok(await employeeService.GetAsync(query, departmentId, status, page, pageSize, cancellationToken)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: ex.Message); }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var employee = await employeeService.GetByIdAsync(id, cancellationToken);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [Authorize(Policy = "employees.manage")]
    public async Task<ActionResult<EmployeeResponse>> Create(CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeeService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { employee.Id }, employee);
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: ex.Message); }
        catch (EmployeeConflictException ex) { return Problem(statusCode: 409, title: ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "employees.manage")]
    public async Task<ActionResult<EmployeeResponse>> Update(Guid id, UpdateEmployeeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var employee = await employeeService.UpdateAsync(id, request, cancellationToken);
            return employee is null ? NotFound() : Ok(employee);
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: ex.Message); }
        catch (EmployeeConflictException ex) { return Problem(statusCode: 409, title: ex.Message); }
        catch (DbUpdateConcurrencyException) { return Problem(statusCode: 409, title: "The employee was changed by another user. Refresh and try again."); }
    }

    [HttpPatch("{id:guid}/active")]
    [Authorize(Policy = "employees.manage")]
    public async Task<ActionResult<EmployeeResponse>> SetActive(Guid id, SetEmployeeActiveRequest request, CancellationToken ct)
    {
        try
        {
            var employee = await employeeService.SetActiveAsync(id, request, ct);
            return employee is null ? NotFound() : Ok(employee);
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, title: ex.Message); }
        catch (EmployeeConflictException ex) { return Problem(statusCode: 409, title: ex.Message); }
        catch (DbUpdateConcurrencyException) { return Problem(statusCode: 409, title: "The employee was changed by another user. Refresh and try again."); }
    }
}
