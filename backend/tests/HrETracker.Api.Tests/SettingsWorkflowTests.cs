using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrETracker.Api.Tests;

public class SettingsWorkflowTests : IAsyncLifetime
{
    private readonly AuthApiFactory factory = new(false, true);
    private HttpClient client = null!;
    public async Task InitializeAsync() { await factory.InitializeAsync(); client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") }); }
    public Task DisposeAsync() { client.Dispose(); factory.Dispose(); return Task.CompletedTask; }
    private async Task Login(string email)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = AuthApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthenticatedUserResponse>())!.AccessToken);
    }
    [Theory]
    [InlineData("hradmin@example.test", false, false, true)]
    [InlineData("viewer@example.test", false, false, false)]
    [InlineData("clothing@example.test", true, true, false)]
    [InlineData("clothingviewer@example.test", true, false, false)]
    [InlineData("inventory@example.test", true, false, false)]
    public async Task Settings_follow_module_ownership(string email, bool clothingRead, bool clothingWrite, bool organizationWrite)
    {
        await Login(email);
        var org = await client.GetFromJsonAsync<JsonElement>("/api/settings");
        Assert.False(org.TryGetProperty("lowStockThreshold", out _));
        Assert.True(org.TryGetProperty("businessDate", out _));
        Assert.Equal(clothingRead ? HttpStatusCode.OK : HttpStatusCode.Forbidden, (await client.GetAsync("/api/clothing/settings")).StatusCode);
        Assert.Equal(clothingWrite ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/clothing/settings", new { })).StatusCode);
        Assert.Equal(organizationWrite ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/settings", new { })).StatusCode);
        Assert.Equal(organizationWrite ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/departments", new { })).StatusCode);
    }
    [Fact]
    public async Task Saves_cannot_change_other_scopes_and_stale_versions_are_rejected()
    {
        await Login("hradmin@example.test");
        var org = (await client.GetFromJsonAsync<OrganizationPreferencesResponse>("/api/settings"))!;
        var result = await client.PutAsJsonAsync("/api/settings", new { companyName = "Updated Company", timeZoneId = "UTC", org.RowVersion, lowStockThreshold = 999, lowStockAlertsEnabled = false });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/settings", new { companyName = "Stale", timeZoneId = "UTC", org.RowVersion })).StatusCode);
        await Login("clothing@example.test");
        var clothing = (await client.GetFromJsonAsync<ClothingSettingsResponse>("/api/clothing/settings"))!;
        Assert.Equal(15, clothing.LowStockThreshold); Assert.True(clothing.LowStockAlertsEnabled);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/clothing/settings", new { lowStockThreshold = 4, lowStockAlertsEnabled = false, clothing.RowVersion, companyName = "Unauthorized Company", timeZoneId = "Invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/clothing/settings", new { lowStockThreshold = 5, lowStockAlertsEnabled = true, clothing.RowVersion })).StatusCode);
        var unchanged = (await client.GetFromJsonAsync<OrganizationPreferencesResponse>("/api/settings"))!;
        Assert.Equal("Updated Company", unchanged.CompanyName); Assert.Equal("UTC", unchanged.TimeZoneId);
    }
    [Fact]
    public async Task Personal_preferences_filter_only_the_current_users_feed_and_persist()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
            foreach (var type in new[] { "LowStock", "RequestProvided", "Info" }) db.Notifications.Add(new Notification { Type = type, Message = type, RelatedEntityType = "InventoryItem", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await Login("clothing@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/auth/profile/preferences", new PersonalNotificationPreferences(false, false))).StatusCode);
        Assert.Equal(new PersonalNotificationPreferences(false, false), await client.GetFromJsonAsync<PersonalNotificationPreferences>("/api/auth/profile/preferences"));
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/notifications");
        Assert.Equal(1, feed.GetProperty("totalCount").GetInt32());
        var snapshot = await client.PostAsync("/api/program/snapshot", null);
        Assert.Equal(1, (await snapshot.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("notifications").GetArrayLength());
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count")).GetProperty("unreadCount").GetInt32());
        await Login("clothingviewer@example.test");
        Assert.Equal(3, (await client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("totalCount").GetInt32());
        Assert.True((await client.GetFromJsonAsync<ClothingSettingsResponse>("/api/clothing/settings"))!.LowStockAlertsEnabled);
    }
    [Fact]
    public async Task Departments_support_unique_names_and_codes_concurrency_and_safe_deactivation()
    {
        await Login("hradmin@example.test");
        var created = await client.PostAsJsonAsync("/api/departments", new SaveDepartmentRequest { Code = " HR ", Name = "Human Resources" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var department = (await created.Content.ReadFromJsonAsync<DepartmentResponse>())!;
        Assert.Equal("HR", department.Code);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/departments", new SaveDepartmentRequest { Code = "HR", Name = "Duplicate" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/departments/{department.Id}", new SaveDepartmentRequest { Code = "HR", Name = "HR department", IsActive = false, RowVersion = department.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/departments/{department.Id}", new SaveDepartmentRequest { Code = "HR", Name = "Stale", RowVersion = department.RowVersion })).StatusCode);
        var production = (await client.GetFromJsonAsync<DepartmentResponse[]>("/api/departments"))!.First(d => d.IsActive);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = "DEPT-TEST", FullName = "Department Test", DepartmentId = production.Id, EnrollmentDate = new DateOnly(2026, 9, 1) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/departments/{production.Id}", new SaveDepartmentRequest { Code = production.Code, Name = production.Name, IsActive = false, RowVersion = production.RowVersion })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>().AuditEvents.CountAsync(a => a.EntityType == "Department"));
    }
}
