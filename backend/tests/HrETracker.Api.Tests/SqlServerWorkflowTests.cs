using HrETracker.Data;
using HrETracker.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace HrETracker.Api.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HR_ETRACKER_SQL_TESTS") != "1") Skip = "Set HR_ETRACKER_SQL_TESTS=1 to run against local SQLEXPRESS in temporary databases.";
    }
}
public class SqlServerWorkflowTests
{
    private static async Task<HttpClient> Client(AuthApiFactory factory)
    {
        await factory.InitializeAsync();
        var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "admin@example.test", Password = AuthApiFactory.Password });
        var token = (await response.Content.ReadFromJsonAsync<AuthenticatedUserResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
    private static async Task<CoatRequestResponse[]> Prepare(HttpClient client, int employeeCount)
    {
        for (var i = 0; i < employeeCount; i++)
        {
            var response = await client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = $"SQL-{i}", FullName = "SQL test", DepartmentId = DemoDepartments.Items[0].Id, EnrollmentDate = new(2026, 4, 2) });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        var receipt = await client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = DemoInventory.Items[3].Id, Quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/coat-requests/run-due-cycle", null)).StatusCode);
        return (await client.GetFromJsonAsync<RequestPage>("/api/coat-requests"))!.Items.ToArray();
    }
    [SqlServerFact]
    public async Task Upgrade_migration_preserves_existing_balances_and_initializes_missing_items()
    {
        using var factory = new AuthApiFactory(true, true);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("20261002103457_AddDepartmentsAndEmployeeLifecycle");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO InventoryBalances (InventoryItemId, QuantityOnHand) VALUES ('c42a7c65-6f0e-45f9-b01d-2c2b43000104', 7);");
            await db.Database.ExecuteSqlRawAsync("INSERT INTO InventoryMovements (Id, InventoryItemId, Type, Quantity, Note, OccurredAtUtc) VALUES (NEWID(), 'c42a7c65-6f0e-45f9-b01d-2c2b43000104', 0, 7, 'Prior receipt', SYSUTCDATETIME());");
        }
        using var client = await Client(factory);
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(7, await finalDb.InventoryBalances.CountAsync());
        Assert.Equal(7, await finalDb.InventoryBalances.Where(b => b.InventoryItemId == DemoInventory.Items[3].Id).Select(b => b.QuantityOnHand).SingleAsync());
        Assert.Equal(7, await finalDb.InventoryMovements.SumAsync(m => m.Quantity));
    }
    [SqlServerFact]
    public async Task Parallel_requests_cannot_overdraw_the_last_unit()
    {
        using var factory = new AuthApiFactory(true, true);
        using var client = await Client(factory);
        var requests = await Prepare(client, 2);
        var responses = await Task.WhenAll(requests.Select(r => client.PostAsJsonAsync($"/api/coat-requests/{r.Id}/provide", new RequestMutation { RowVersion = r.RowVersion })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        await VerifySingleAllocation(factory);
    }
    [SqlServerFact]
    public async Task Parallel_provision_of_one_request_completes_exactly_once()
    {
        using var factory = new AuthApiFactory(true, true);
        using var client = await Client(factory);
        var request = Assert.Single(await Prepare(client, 1));
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync($"/api/coat-requests/{request.Id}/provide", new RequestMutation { RowVersion = request.RowVersion })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        await VerifySingleAllocation(factory);
    }
    [SqlServerFact]
    public async Task Failed_allocation_rolls_back_balance_completion_notification_and_audit()
    {
        using var factory = new AuthApiFactory(true, true);
        using var client = await Client(factory);
        var r = Assert.Single(await Prepare(client, 1));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER TestRejectAllocation ON InventoryMovements AFTER INSERT AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE [Type] = 1) THROW 51001, 'Injected test failure', 1; END;");
        var result = await client.PostAsJsonAsync($"/api/coat-requests/{r.Id}/provide", new RequestMutation { RowVersion = r.RowVersion });
        Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        Assert.Equal(1, await db.InventoryBalances.Where(b => b.InventoryItemId == DemoInventory.Items[3].Id).Select(b => b.QuantityOnHand).SingleAsync());
        Assert.Equal(0, await db.CoatRequests.CountAsync(r => r.Status == CoatRequestStatus.Provided));
        Assert.Equal(0, await db.InventoryMovements.CountAsync(m => m.Type == InventoryMovementType.Allocation));
        Assert.Equal(0, await db.AuditEvents.CountAsync(a => a.Action == "CoatRequest.Provided"));
        Assert.Equal(0, await db.Notifications.CountAsync(n => n.Type == "RequestProvided"));
    }
    private static async Task VerifySingleAllocation(AuthApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(0, await db.InventoryBalances.Where(b => b.InventoryItemId == DemoInventory.Items[3].Id).Select(b => b.QuantityOnHand).SingleAsync());
        Assert.Equal(1, await db.CoatRequests.CountAsync(r => r.Status == CoatRequestStatus.Provided));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(m => m.Type == InventoryMovementType.Allocation));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "CoatRequest.Provided"));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "RequestProvided"));
    }
    [SqlServerFact]
    public async Task Parallel_generation_and_receipts_are_idempotent_and_do_not_lose_stock()
    {
        using var factory = new AuthApiFactory(true, true);
        using var client = await Client(factory);
        var employee = await client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = "SQL-GEN", FullName = "SQL test", DepartmentId = DemoDepartments.Items[0].Id, EnrollmentDate = new(2024, 1, 1) });
        Assert.Equal(HttpStatusCode.Created, employee.StatusCode);
        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsync("/api/coat-requests/run-due-cycle", null)));
        Assert.All(runs, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var created = 0;
        foreach (var r in runs) created += (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32();
        Assert.Equal(1, created);
        var receipts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = DemoInventory.Items[3].Id, Quantity = 1 })));
        Assert.All(receipts, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(4, await db.InventoryBalances.Where(b => b.InventoryItemId == DemoInventory.Items[3].Id).Select(b => b.QuantityOnHand).SingleAsync());
        Assert.Equal(4, await db.InventoryMovements.SumAsync(m => m.Quantity));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "RequestDue"));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "LowStock" && n.ResolvedAtUtc == null));
    }
}
