using System.Net;
using System.Net.Http.Json;
using System.Text;
using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrETracker.Api.Tests;

public class SqlServerDashboardImportTests
{
    private static async Task<HttpClient> Client(AuthApiFactory factory)
    {
        await factory.InitializeAsync();
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "admin@example.test", Password = AuthApiFactory.Password });
        var tokens = (await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        return client;
    }
    private static async Task<ImportSummary> Stage(HttpClient client)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Employee ID,Full Name,Department,Enrollment Date\nFIRST,Name,Operations,2026-04-02\nSECOND,Name,Warehouse,2026-04-02\n")), "file", "employees.csv");
        var response = await client.PostAsync("/api/employee-imports", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportSummary>())!;
    }

    [SqlServerFact]
    public async Task Parallel_commit_is_idempotent_and_dashboard_queries_real_issues()
    {
        using var factory = new AuthApiFactory(true, true);
        using var client = await Client(factory);
        var batch = await Stage(client);
        var commits = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null)));
        Assert.All(commits, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var retryUpload = await Stage(client);
        Assert.Equal("Invalid", retryUpload.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/coat-requests/run-due-cycle", null)).StatusCode);
        var receipt = await client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = DemoInventory.Items[3].Id, Quantity = 8 });
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        var requests = (await client.GetFromJsonAsync<RequestPage>("/api/coat-requests"))!;
        var request = Assert.Single(requests.Items, r => r.EmployeeNumber == "FIRST");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/coat-requests/{request.Id}/provide", new RequestMutation { RowVersion = request.RowVersion })).StatusCode);
        var dashboard = (await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard?year=2026"))!;
        Assert.Equal(2, dashboard.ActiveEmployees); Assert.Equal(7, dashboard.QuantityOnHand);
        Assert.Equal(1, dashboard.ProvidedRequests); Assert.Equal(1, dashboard.OutOfStockRequests);
        Assert.Equal(1, dashboard.MonthlyIssues.Sum(m => m.Quantity));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(2, await db.Employees.CountAsync());
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "EmployeeImport.Committed"));
        Assert.Equal(2, await db.ImportRows.CountAsync(r => r.EmployeeId != null));
    }

    [SqlServerFact]
    public async Task Failed_import_rolls_back_all_employees_and_can_be_retried()
    {
        using var factory = new AuthApiFactory(true, true);
        using var client = await Client(factory);
        var batch = await Stage(client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Employees ADD CONSTRAINT CK_TestRejectSecondEmployee CHECK (EmployeeNumber <> 'SECOND');");
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null)).StatusCode);
        Assert.Equal(0, await db.Employees.CountAsync());
        Assert.Equal(0, await db.ImportRows.CountAsync(r => r.EmployeeId != null));
        Assert.Equal(0, await db.AuditEvents.CountAsync(a => a.Action == "Employee.Created" || a.Action == "EmployeeImport.Committed"));
        Assert.Equal("Validated", (await db.ImportBatches.SingleAsync()).Status);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Employees DROP CONSTRAINT CK_TestRejectSecondEmployee;");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null)).StatusCode);
        Assert.Equal(2, await db.Employees.CountAsync());
    }
}
