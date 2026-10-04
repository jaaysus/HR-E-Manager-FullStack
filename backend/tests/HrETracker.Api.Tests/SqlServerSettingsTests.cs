using System.Net;
using System.Net.Http.Json;
using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrETracker.Api.Tests;

public class SqlServerSettingsTests
{
    [SqlServerFact]
    public async Task Scoped_settings_departments_and_personal_preferences_persist_with_SQL_rowversions()
    {
        using var factory = new AuthApiFactory(true, true);
        await factory.InitializeAsync();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "admin@example.test", Password = AuthApiFactory.Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticatedUserResponse>())!.AccessToken);
        var settings = (await client.GetFromJsonAsync<OrganizationPreferencesResponse>("/api/settings"))!;
        var outcomes = await Task.WhenAll(
            client.PutAsJsonAsync("/api/settings", new OrganizationPreferencesInput { CompanyName = "SQL company", TimeZoneId = "UTC", RowVersion = settings.RowVersion }),
            client.PutAsJsonAsync("/api/clothing/settings", new ClothingSettingsInput { LowStockAlertsEnabled = false, LowStockThreshold = 3, RowVersion = settings.RowVersion }));
        Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(outcomes, r => r.StatusCode == HttpStatusCode.Conflict);
        var result = await client.PostAsJsonAsync("/api/departments", new SaveDepartmentRequest { Code = "SQL_DEPT", Name = "SQL Department" });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var department = (await result.Content.ReadFromJsonAsync<DepartmentResponse>())!;
        Assert.NotEmpty(department.RowVersion);
        var update = new SaveDepartmentRequest { Code = department.Code, Name = "Updated SQL Department", RowVersion = department.RowVersion };
        var updated = await client.PutAsJsonAsync($"/api/departments/{department.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var latestDepartment = (await updated.Content.ReadFromJsonAsync<DepartmentResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/departments/{department.Id}", update)).StatusCode);
        var race = await Task.WhenAll(
            client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = "SQL-DEPT-RACE", FullName = "SQL Employee", DepartmentId = department.Id, EnrollmentDate = new DateOnly(2026, 9, 1) }),
            client.PutAsJsonAsync($"/api/departments/{department.Id}", new SaveDepartmentRequest { Code = latestDepartment.Code, Name = latestDepartment.Name, IsActive = false, RowVersion = latestDepartment.RowVersion }));
        Assert.True((race[0].StatusCode == HttpStatusCode.Created && race[1].StatusCode == HttpStatusCode.Conflict)
            || (race[0].StatusCode == HttpStatusCode.BadRequest && race[1].StatusCode == HttpStatusCode.OK));
        Assert.Equal(new PersonalNotificationPreferences(true, true), await client.GetFromJsonAsync<PersonalNotificationPreferences>("/api/auth/profile/preferences"));
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/auth/profile/preferences", new PersonalNotificationPreferences(false, true))).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Email == "admin@example.test");
        Assert.False(user.NotifyLowStock); Assert.True(user.NotifyClothingActivity);
        Assert.Equal("Updated SQL Department", (await db.Departments.SingleAsync(d => d.Id == department.Id)).Name);
        Assert.Equal(race[1].StatusCode == HttpStatusCode.OK ? 3 : 2, await db.AuditEvents.CountAsync(a => a.EntityType == "Department"));
        Assert.False(await db.Employees.Include(e => e.Department).AnyAsync(e => e.IsActive && !e.Department.IsActive));
    }
}
