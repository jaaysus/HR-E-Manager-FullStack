using HrETracker.Data;
using HrETracker.Models;
using HrETracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace HrETracker.Api.Tests;

public sealed class EmployeeTests : IAsyncLifetime
{
    private readonly AuthApiFactory factory = new();
    private HttpClient client = null!;
    private readonly Guid departmentId = DemoDepartments.Items[0].Id;
    public async Task InitializeAsync()
    {
        await factory.InitializeAsync();
        client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await Login("admin@example.test");
    }
    public Task DisposeAsync() { client.Dispose(); factory.Dispose(); return Task.CompletedTask; }
    private async Task Login(string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = AuthApiFactory.Password });
        var tokens = (await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }
    private CreateEmployeeRequest Create(string number = "EMP-100", Guid? department = null) => new()
    {
        EmployeeNumber = number, FullName = " Test Employee ", DepartmentId = department ?? departmentId,
        EnrollmentDate = new(2026, 1, 1), Notes = "private notes"
    };
    private async Task<EmployeeResponse> Add(string number = "EMP-100", Guid? department = null)
    {
        var response = await client.PostAsJsonAsync("/api/employees", Create(number, department));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>())!;
    }
    private static UpdateEmployeeRequest Update(EmployeeResponse e, string name = "Updated Name") => new()
    {
        FullName = name, DepartmentId = e.DepartmentId, EnrollmentDate = e.EnrollmentDate, RowVersion = e.RowVersion
    };

    [Fact]
    public async Task Departments_search_paging_and_filters_return_persisted_data()
    {
        Assert.Equal(6, (await client.GetFromJsonAsync<DepartmentResponse[]>("/api/departments"))!.Length);
        await Add("EMP-101"); await Add("EMP-102"); await Add("EMP-103", DemoDepartments.Items[1].Id);
        var page = (await client.GetFromJsonAsync<EmployeePage>($"/api/employees?query=Test&departmentId={departmentId}&page=2&pageSize=1"))!;
        Assert.Equal(2, page.TotalCount); Assert.Equal("EMP-102", Assert.Single(page.Items).EmployeeNumber);
        Assert.Empty((await client.GetFromJsonAsync<EmployeePage>("/api/employees?query=missing"))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/employees?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/employees?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/employees?status=invalid")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<EmployeePage>("/api/employees?page=2147483647"))!.Items);
    }
    [Fact]
    public async Task Updates_and_deactivation_use_versions_and_atomic_redacted_audits()
    {
        var employee = await Add(" emp-100 ");
        Assert.Equal("EMP-100", employee.EmployeeNumber); Assert.Equal("Test Employee", employee.FullName);
        Assert.NotEmpty(employee.RowVersion);
        var updated = await client.PutAsJsonAsync($"/api/employees/{employee.Id}", Update(employee));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var fresh = (await updated.Content.ReadFromJsonAsync<EmployeeResponse>())!;
        Assert.NotEqual(employee.RowVersion, fresh.RowVersion);
        var stale = await client.PutAsJsonAsync($"/api/employees/{employee.Id}", Update(employee));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/employees/{employee.Id}/active", new SetEmployeeActiveRequest { RowVersion = employee.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/employees/{employee.Id}/active", new SetEmployeeActiveRequest { RowVersion = fresh.RowVersion })).StatusCode);
        var inactive = (await client.GetFromJsonAsync<EmployeeResponse>($"/api/employees/{employee.Id}"))!;
        Assert.False(inactive.IsActive);
        Assert.Empty((await client.GetFromJsonAsync<EmployeePage>("/api/employees"))!.Items);
        Assert.Single((await client.GetFromJsonAsync<EmployeePage>("/api/employees?status=inactive"))!.Items);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/employees/{employee.Id}/active", new SetEmployeeActiveRequest { IsActive = true, RowVersion = inactive.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/employees", Create("EMP-100"))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        var audit = await db.AuditEvents.OrderBy(a => a.OccurredAtUtc).ToListAsync();
        Assert.Equal(3, audit.Count);
        Assert.All(audit, a => { Assert.NotNull(a.ActorUserId); Assert.NotNull(a.CorrelationId); Assert.DoesNotContain("private notes", a.AfterJson); Assert.DoesNotContain("Test Employee", a.AfterJson); });
        Assert.Equal(audit[0].ActorUserId, (await db.Employees.SingleAsync()).CreatedByUserId);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<IRequestCycleService>().CreateDueRequestsAsync(default));
        audit[0].Action = "Tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Invalid_fields_and_departments_are_rejected_and_viewers_cannot_write()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/employees", Create(department: Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/employees", new { employeeNumber = "EMP-1", fullName = "  ", departmentId, enrollmentDate = "2026-01-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/employees", new { employeeNumber = "EMP-1", fullName = "Valid", departmentId })).StatusCode);
        var employee = await Add();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/employees/{employee.Id}", new { fullName = "Valid", departmentId, enrollmentDate = "2026-01-01", rowVersion = "invalid" })).StatusCode);
        await Login("viewer@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/departments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/employees/{employee.Id}", Update(employee))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PatchAsJsonAsync($"/api/employees/{employee.Id}/active", new { isActive = false, employee.RowVersion })).StatusCode);
    }
    [Fact]
    public async Task Empty_app_has_reference_departments_and_no_mock_employees()
    {
        Assert.Empty((await client.GetFromJsonAsync<EmployeePage>("/api/employees?status=all"))!.Items);
        Assert.Equal(6, (await client.GetFromJsonAsync<DepartmentResponse[]>("/api/departments"))!.Length);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(0, await db.AuditEvents.CountAsync());
    }
}
