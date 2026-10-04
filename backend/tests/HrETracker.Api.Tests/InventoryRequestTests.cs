using HrETracker.Data;
using HrETracker.Models;
using HrETracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
namespace HrETracker.Api.Tests;

public class InventoryRequestTests : IAsyncLifetime
{
    protected readonly AuthApiFactory Factory;
    protected HttpClient Client = null!;
    private readonly Guid itemId = DemoInventory.Items[3].Id;
    public InventoryRequestTests() : this(false) { }
    protected InventoryRequestTests(bool sql) => Factory = new(sql, true);
    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();
        Client = Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        await Login("admin@example.test");
    }
    public Task DisposeAsync() { Client.Dispose(); Factory.Dispose(); return Task.CompletedTask; }
    protected async Task Login(string email)
    {
        Client.DefaultRequestHeaders.Authorization = null;
        var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = AuthApiFactory.Password });
        var tokens = (await login.Content.ReadFromJsonAsync<AuthenticatedUserResponse>())!;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }
    protected async Task AddEmployee(string number, DateOnly enrollment, Guid? department = null)
    {
        var result = await Client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = number, FullName = "Employee", DepartmentId = department ?? DemoDepartments.Items[0].Id, EnrollmentDate = enrollment });
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }
    protected async Task<InventoryItemResponse> Receipt(int quantity, Guid? id = null)
    {
        var result = await Client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = id ?? itemId, Quantity = quantity });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
    }
    protected async Task<int> Run()
    {
        var result = await Client.PostAsync("/api/coat-requests/run-due-cycle", null);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetInt32();
    }
    protected async Task<CoatRequestResponse[]> Requests() => (await Client.GetFromJsonAsync<RequestPage>("/api/coat-requests"))!.Items.ToArray();
    protected async Task<InventoryItemResponse> Item() => (await Client.GetFromJsonAsync<InventoryItemResponse[]>("/api/inventory/items"))!.Single(i => i.Id == itemId);
    [Fact]
    public async Task Movement_history_identifies_coat_recipient_and_restock_receiver_even_when_inactive()
    {
        await AddEmployee("RECIPIENT", new(2026, 4, 2));
        await Login("clothing@example.test");
        await Receipt(2);
        await Run();
        var request = Assert.Single(await Requests());
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{request.Id}/provide",
            new RequestMutation { RowVersion = request.RowVersion, InventoryItemId = itemId })).StatusCode);
        var item = await Item();
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/inventory/adjustments",
            new RecordAdjustmentRequest { InventoryItemId = itemId, Quantity = -1, Note = "Count correction", RowVersion = item.RowVersion })).StatusCode);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
            (await db.Employees.SingleAsync(e => e.EmployeeNumber == "RECIPIENT")).IsActive = false;
            await db.SaveChangesAsync();
        }
        var response = await Client.PostAsync("/api/program/snapshot", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<JsonElement>();
        var movements = snapshot.GetProperty("movements").EnumerateArray().ToArray();
        var allocation = Assert.Single(movements, m => m.GetProperty("type").GetString() == "allocation");
        Assert.Equal("Employee", allocation.GetProperty("recipientName").GetString());
        Assert.Equal("RECIPIENT", allocation.GetProperty("recipientEmployeeNumber").GetString());
        var arrival = Assert.Single(movements, m => m.GetProperty("type").GetString() == "arrival");
        Assert.Equal("ClothingManager", arrival.GetProperty("recipientName").GetString());
        Assert.Equal(JsonValueKind.Null, arrival.GetProperty("recipientEmployeeNumber").ValueKind);
        var adjustment = Assert.Single(movements, m => m.GetProperty("type").GetString() == "adjustment");
        Assert.Equal(JsonValueKind.Null, adjustment.GetProperty("recipientName").ValueKind);
    }
    [Theory]
    [InlineData("2025-08-31", "2026-02-27", 1)]
    [InlineData("2025-08-31", "2026-02-28", 1)]
    [InlineData("2023-08-31", "2024-02-29", 1)]
    [InlineData("2025-08-31", "2026-08-30", 2)]
    [InlineData("2025-08-31", "2026-08-31", 2)]
    [InlineData("2027-01-01", "2026-10-02", 0)]
    public void Demo_calendar_month_cycles(string enrolled, string today, int expected) => Assert.Equal(expected, RequestCycleService.GetCurrentCycle(DateOnly.Parse(enrolled), DateOnly.Parse(today)));

    [Fact]
    public async Task Only_current_cycle_is_generated_without_automatic_variant_and_retries_are_idempotent()
    {
        await AddEmployee("CYCLE", new(2024, 1, 1), DemoDepartments.Items[2].Id);
        Assert.Equal(1, await Run()); Assert.Equal(0, await Run());
        var requests = await Requests();
        Assert.Equal(5, Assert.Single(requests).CycleNumber);
        Assert.All(requests, r => Assert.Null(r.ItemSku));
        var cancel = requests[0];
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{cancel.Id}/cancel", new RequestMutation { RowVersion = cancel.RowVersion })).StatusCode);
        Assert.Equal(0, await Run());
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/coat-requests/{cancel.Id}/provide", new RequestMutation { RowVersion = cancel.RowVersion })).StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "RequestDue"));
        Assert.Empty(await db.InventoryMovements.ToListAsync());
    }
    [Fact]
    public async Task Receipts_adjustments_alert_recovery_and_ledger_are_atomic()
    {
        var first = await Receipt(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = itemId, Quantity = 0 })).StatusCode);
        var second = await Receipt(1);
        Assert.NotEqual(first.RowVersion, second.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/inventory/adjustments", new RecordAdjustmentRequest { InventoryItemId = itemId, Quantity = -1, Note = "count", RowVersion = first.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync("/api/inventory/adjustments", new RecordAdjustmentRequest { InventoryItemId = itemId, Quantity = -3, Note = "count", RowVersion = second.RowVersion })).StatusCode);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "LowStock" && n.ResolvedAtUtc == null));
            Assert.Equal(2, await db.InventoryMovements.CountAsync());
        }
        var high = await Receipt(16);
        var result = await Client.PostAsJsonAsync("/api/inventory/adjustments", new RecordAdjustmentRequest { InventoryItemId = itemId, Quantity = -17, Note = "Physical count", RowVersion = high.RowVersion });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
            Assert.Equal(2, await db.Notifications.CountAsync(n => n.Type == "LowStock"));
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "LowStock" && n.ResolvedAtUtc == null));
            Assert.Equal(await db.InventoryMovements.SumAsync(m => m.Quantity), (await Item()).QuantityOnHand);
            var movement = await db.InventoryMovements.FirstAsync(); movement.Note = "tampered";
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        var ledger = await Client.GetAsync($"/api/inventory/items/{itemId}/movements?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, ledger.StatusCode);
    }
    [Fact]
    public async Task Active_request_badge_counts_pending_and_out_of_stock_and_excludes_finished_and_inactive()
    {
        async Task<int> Count() => (await Client.GetFromJsonAsync<JsonElement>("/api/coat-requests/active-count")).GetProperty("activeCount").GetInt32();
        Assert.Equal(0, await Count());
        await AddEmployee("BADGE-A", new(2024, 1, 1));
        await AddEmployee("BADGE-B", new(2024, 1, 1));
        await AddEmployee("BADGE-C", new(2024, 1, 1));
        await Run();
        Assert.Equal(3, await Count());
        var rows = await Requests();
        var cancelled = rows.Single(r => r.EmployeeNumber == "BADGE-A");
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{cancelled.Id}/cancel", new RequestMutation { RowVersion = cancelled.RowVersion })).StatusCode);
        var waiting = rows.Single(r => r.EmployeeNumber == "BADGE-C");
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"/api/coat-requests/{waiting.Id}/assign-item", new AssignRequestItem { RowVersion = waiting.RowVersion, InventoryItemId = itemId })).StatusCode);
        Assert.Equal(CoatRequestStatus.OutOfStock, (await Requests()).Single(r => r.EmployeeNumber == "BADGE-C").Status);
        Assert.Equal(2, await Count());
        await Receipt(1);
        var provided = (await Requests()).Single(r => r.EmployeeNumber == "BADGE-B");
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{provided.Id}/provide", new RequestMutation { RowVersion = provided.RowVersion, InventoryItemId = itemId })).StatusCode);
        Assert.Equal(1, await Count());
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        (await db.Employees.SingleAsync(e => e.EmployeeNumber == "BADGE-C")).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(0, await Count());
        await Login("viewer@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.GetAsync("/api/coat-requests/active-count")).StatusCode);
    }
    [Fact]
    public async Task Provision_replenishment_and_versions_create_exactly_one_completion()
    {
        await AddEmployee("ISSUE", new(2026, 4, 2));
        Assert.Equal(1, await Run());
        var unassigned = Assert.Single(await Requests()); Assert.Equal(CoatRequestStatus.Pending, unassigned.Status);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsJsonAsync($"/api/coat-requests/{unassigned.Id}/assign-item", new AssignRequestItem { InventoryItemId = itemId, RowVersion = unassigned.RowVersion })).StatusCode);
        var old = Assert.Single(await Requests()); Assert.Equal(CoatRequestStatus.OutOfStock, old.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/coat-requests/{old.Id}/provide", new RequestMutation { RowVersion = old.RowVersion })).StatusCode);
        await Receipt(1);
        var fresh = Assert.Single(await Requests()); Assert.Equal(CoatRequestStatus.Pending, fresh.Status);
        Assert.NotEqual(old.RowVersion, fresh.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/coat-requests/{old.Id}/provide", new RequestMutation { RowVersion = old.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{fresh.Id}/provide", new RequestMutation { RowVersion = fresh.RowVersion, InventoryItemId = itemId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/coat-requests/{fresh.Id}/provide", new RequestMutation { RowVersion = fresh.RowVersion, InventoryItemId = itemId })).StatusCode);
        Assert.Equal(0, await Run()); Assert.Equal(0, (await Item()).QuantityOnHand);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(1, await db.InventoryMovements.CountAsync(m => m.Type == InventoryMovementType.Allocation));
        Assert.Equal(1, await db.Notifications.CountAsync(n => n.Type == "RequestProvided"));
        Assert.Equal(1, await db.AuditEvents.CountAsync(n => n.Action == "CoatRequest.Provided"));
        Assert.NotNull((await db.CoatRequests.SingleAsync()).ProvidedByUserId);
    }
    [Fact]
    public async Task Variants_support_creation_editing_retirement_and_stale_update_checks()
    {
        var created = await Client.PostAsJsonAsync("/api/inventory/items", new CoatVariantInput { Name = "Test coat", Color = "Blue", Size = "M" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var item = (await created.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
        Assert.Equal("Blue", item.Color); Assert.Equal("M", item.Size);
        Assert.False(string.IsNullOrWhiteSpace(item.Sku));
        var edit = new CoatVariantInput { Name = "Updated coat", Color = "Red", Size = "XL", RowVersion = item.RowVersion };
        var updated = await Client.PutAsJsonAsync($"/api/inventory/items/{item.Id}", edit);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"/api/inventory/items/{item.Id}", edit)).StatusCode);
        var fresh = (await updated.Content.ReadFromJsonAsync<InventoryItemResponse>())!;
        Assert.Equal("Red", fresh.Color); Assert.Equal("XL", fresh.Size);
        Assert.Equal(item.Sku, fresh.Sku);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/inventory/items/{item.Id}?rowVersion={Uri.EscapeDataString(fresh.RowVersion)}")).StatusCode);
        Assert.False((await Client.GetFromJsonAsync<InventoryItemResponse[]>("/api/inventory/items"))!.Single(i => i.Id == item.Id).IsActive);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/inventory/item-rules")).StatusCode);
    }
    [Fact]
    public async Task Any_department_can_take_any_variant_and_history_survives_retirement()
    {
        await AddEmployee("CHOICE", new(2026, 4, 2), DemoDepartments.Items[2].Id);
        await Receipt(1);
        await Run();
        var request = Assert.Single(await Requests());
        Assert.Null(request.InventoryItemId);
        var provided = await Client.PostAsJsonAsync($"/api/coat-requests/{request.Id}/provide", new RequestMutation { InventoryItemId = itemId, RowVersion = request.RowVersion });
        Assert.Equal(HttpStatusCode.OK, provided.StatusCode);
        Assert.Equal(itemId, (await provided.Content.ReadFromJsonAsync<CoatRequestResponse>())!.InventoryItemId);
        var item = await Item();
        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/inventory/items/{item.Id}?rowVersion={Uri.EscapeDataString(item.RowVersion)}")).StatusCode);
        Assert.Equal(itemId, Assert.Single(await Requests()).InventoryItemId);
        Assert.Equal(CoatRequestStatus.Provided, Assert.Single(await Requests()).Status);
    }
    [Fact]
    public async Task Settings_and_notification_reads_are_persisted_and_user_specific()
    {
        await Receipt(1);
        var settings = (await Client.GetFromJsonAsync<ClothingSettingsResponse>("/api/clothing/settings"))!;
        var update = new ClothingSettingsInput { LowStockAlertsEnabled = false, LowStockThreshold = 2, RowVersion = settings.RowVersion };
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync("/api/clothing/settings", update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync("/api/clothing/settings", update)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync("/api/notifications/read-all", null)).StatusCode);
        var read = (await Client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items")[0];
        Assert.NotEqual(JsonValueKind.Null, read.GetProperty("readAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, read.GetProperty("resolvedAtUtc").ValueKind);
        await Login("clothingviewer@example.test");
        var unread = (await Client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, unread.GetProperty("readAtUtc").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = itemId, Quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PostAsync("/api/coat-requests/run-due-cycle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Client.PutAsJsonAsync("/api/clothing/settings", update)).StatusCode);
    }

    private async Task<JsonElement> Snapshot()
    {
        var response = await Client.PostAsync("/api/program/snapshot", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private sealed class MovableClock : TimeProvider
    {
        public DateTimeOffset Date { get; set; } = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Date;
    }

    [Fact]
    public async Task Minute_interval_starts_after_future_enrollment_and_reports_the_exact_next_time()
    {
        await AddEmployee("FUTURE-MINUTE", new(2026, 10, 6));
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        var settings = await db.OrganizationSettings.SingleAsync();
        settings.EligibilityUnit = "Minutes"; settings.EligibilityInterval = 1;
        settings.EligibilityEffectiveAtUtc = new(2026, 10, 4, 14, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        var clock = new MovableClock { Date = new(2026, 10, 4, 14, 30, 0, TimeSpan.Zero) };
        var service = new RequestCycleService(db, scope.ServiceProvider.GetRequiredService<StockWorkflow>(), clock);
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        var employee = await db.Employees.SingleAsync(e => e.EmployeeNumber == "FUTURE-MINUTE");
        var enrollmentUtc = TimeZoneInfo.ConvertTimeToUtc(employee.EnrollmentDate.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId));
        Assert.Equal(enrollmentUtc.AddMinutes(1), RequestCycleService.NextEligibilityAtUtc(employee, [], settings, clock.Date));
        clock.Date = new DateTimeOffset(enrollmentUtc).AddSeconds(59);
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        clock.Date = clock.Date.AddSeconds(1);
        Assert.Equal(1, await service.CreateDueRequestsAsync(default));
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        Assert.Equal(1, (await db.CoatRequests.SingleAsync()).CycleNumber);
    }

    [Fact]
    public async Task Minute_policy_waits_one_minute_then_allows_one_issue_per_interval()
    {
        await AddEmployee("MINUTE", new(2024, 1, 1));
        await Receipt(2);
        await Run();
        var original = Assert.Single(await Requests());
        var settings = (await Client.GetFromJsonAsync<ClothingSettingsResponse>("/api/clothing/settings"))!;
        var change = new ClothingSettingsInput { EligibilityInterval = 1, EligibilityUnit = "Minutes", LowStockAlertsEnabled = true, LowStockThreshold = 15, RowVersion = settings.RowVersion };
        var saved = await Client.PutAsJsonAsync("/api/clothing/settings", change);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Minutes", (await saved.Content.ReadFromJsonAsync<ClothingSettingsResponse>())!.EligibilityUnit);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync("/api/clothing/settings", change)).StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        var epoch = (await db.OrganizationSettings.SingleAsync()).EligibilityEffectiveAtUtc!.Value;
        var clock = new MovableClock { Date = new DateTimeOffset(DateTime.SpecifyKind(epoch, DateTimeKind.Utc)).AddSeconds(59) };
        var service = new RequestCycleService(db, scope.ServiceProvider.GetRequiredService<StockWorkflow>(), clock);
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        Assert.Equal(CoatRequestStatus.Cancelled, (await db.CoatRequests.AsNoTracking().SingleAsync()).Status);
        clock.Date = clock.Date.AddSeconds(1);
        Assert.Equal(1, await service.CreateDueRequestsAsync(default));
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        var pending = await db.CoatRequests.AsNoTracking().SingleAsync(r => r.Status == CoatRequestStatus.Pending);
        Assert.True(pending.CycleNumber > original.CycleNumber);
        var issued = await service.ProvideAsync(pending.Id, new RequestMutation { RowVersion = Convert.ToBase64String(pending.RowVersion), InventoryItemId = itemId }, default);
        Assert.Equal(CoatRequestStatus.Provided, issued!.Status);
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        await Assert.ThrowsAsync<WorkflowConflictException>(() => service.ProvideAsync(pending.Id, new RequestMutation { RowVersion = issued.RowVersion, InventoryItemId = itemId }, default));
        clock.Date = clock.Date.AddMinutes(1);
        Assert.Equal(1, await service.CreateDueRequestsAsync(default));
        Assert.Equal(1, await db.InventoryMovements.CountAsync(m => m.Type == InventoryMovementType.Allocation));
        Assert.Equal(1, (await Item()).QuantityOnHand);
    }

    [Theory]
    [InlineData(0, "Minutes")]
    [InlineData(-1, "Days")]
    [InlineData(10001, "Months")]
    [InlineData(1, "Season")]
    public async Task Invalid_eligibility_intervals_are_rejected(int interval, string unit)
    {
        var settings = (await Client.GetFromJsonAsync<ClothingSettingsResponse>("/api/clothing/settings"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync("/api/clothing/settings", new ClothingSettingsInput { EligibilityInterval = interval, EligibilityUnit = unit, RowVersion = settings.RowVersion })).StatusCode);
    }

    [Fact]
    public async Task Dates_do_not_select_variants_and_new_cycles_supersede_old_requests()
    {
        await AddEmployee("SEASON", new(2024, 1, 15), DemoDepartments.Items[2].Id);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        var clock = new MovableClock();
        var service = new RequestCycleService(db, scope.ServiceProvider.GetRequiredService<StockWorkflow>(), clock);
        Assert.Equal(1, await service.CreateDueRequestsAsync(default));
        var summer = await db.CoatRequests.AsNoTracking().SingleAsync();
        Assert.Null(summer.InventoryItemId);
        Assert.Equal(5, summer.CycleNumber);
        clock.Date = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
        var winter = await db.CoatRequests.AsNoTracking().SingleAsync();
        Assert.Equal(summer.Id, winter.Id);
        Assert.Null(winter.InventoryItemId);
        clock.Date = new(2027, 1, 2, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(1, await service.CreateDueRequestsAsync(default));
        var rows = await db.CoatRequests.AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(CoatRequestStatus.Cancelled, rows.Single(r => r.CycleNumber == 5).Status);
        Assert.Equal(CoatRequestStatus.Pending, rows.Single(r => r.CycleNumber == 6).Status);
        Assert.Equal(0, await service.CreateDueRequestsAsync(default));
    }

    [Fact]
    public async Task Editing_enrollment_after_provision_does_not_issue_another_coat_for_a_completed_cycle()
    {
        await AddEmployee("DATE-EDIT", new(2024, 1, 15));
        await Receipt(1);
        await Snapshot();
        var request = Assert.Single(await Requests());
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{request.Id}/provide", new RequestMutation { RowVersion = request.RowVersion, InventoryItemId = itemId })).StatusCode);
        var e = (await Client.GetFromJsonAsync<EmployeePage>("/api/employees"))!.Items.Single();
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync($"/api/employees/{e.Id}", new UpdateEmployeeRequest { FullName = e.FullName, DepartmentId = e.DepartmentId, EnrollmentDate = new(2026, 4, 15), RowVersion = e.RowVersion })).StatusCode);
        var snapshot = await Snapshot();
        Assert.Equal("provided", snapshot.GetProperty("employees")[0].GetProperty("coatStatus").GetString());
        Assert.Single(await Requests());
        Assert.Equal(0, (await Item()).QuantityOnHand);
    }

    [Fact]
    public async Task Snapshot_reconciles_one_current_request_and_updates_all_views_after_provision()
    {
        await AddEmployee("DEMO-OLD", new(2024, 1, 15));
        await AddEmployee("DEMO-NEW", new(2026, 9, 1));
        var snapshot = await Snapshot();
        var old = snapshot.GetProperty("employees").EnumerateArray().Single(e => e.GetProperty("employeeNumber").GetString() == "DEMO-OLD");
        Assert.Equal(5, old.GetProperty("currentCycle").GetInt32());
        Assert.Equal("requested", old.GetProperty("coatStatus").GetString());
        Assert.Equal(JsonValueKind.Null, old.GetProperty("requiredCoatId").ValueKind);
        Assert.Equal("none", snapshot.GetProperty("employees").EnumerateArray().Single(e => e.GetProperty("employeeNumber").GetString() == "DEMO-NEW").GetProperty("coatStatus").GetString());
        Assert.Single(await Requests());
        await Snapshot();
        Assert.Single(await Requests());
        await Receipt(1);
        var request = Assert.Single(await Requests());
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync($"/api/coat-requests/{request.Id}/provide", new RequestMutation { RowVersion = request.RowVersion, InventoryItemId = itemId })).StatusCode);
        var complete = await Snapshot();
        Assert.Equal("provided", complete.GetProperty("employees").EnumerateArray().Single(e => e.GetProperty("employeeNumber").GetString() == "DEMO-OLD").GetProperty("coatStatus").GetString());
        Assert.Contains(complete.GetProperty("notifications").EnumerateArray(), n => n.GetProperty("message").GetString()!.Contains("Employee (DEMO-OLD)") && n.GetProperty("type").GetString() == "success");
        Assert.Single(complete.GetProperty("movements").EnumerateArray(), m => m.GetProperty("type").GetString() == "allocation");
        await Client.PostAsync("/api/notifications/read-all", null);
        Assert.All((await Snapshot()).GetProperty("notifications").EnumerateArray(), n => Assert.True(n.GetProperty("read").GetBoolean()));
    }

    [Fact]
    public async Task Arrival_date_is_saved_and_invalid_dates_roll_back_stock()
    {
        var receipt = await Client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = itemId, Quantity = 3, ArrivalDate = new(2026, 9, 1), Note = "Invoice 42" });
        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        var arrival = Assert.Single((await Snapshot()).GetProperty("movements").EnumerateArray());
        Assert.Equal("2026-09-01", arrival.GetProperty("date").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/inventory/receipts", new RecordReceiptRequest { InventoryItemId = itemId, Quantity = 2, ArrivalDate = new(2099, 1, 1) })).StatusCode);
        Assert.Equal(3, (await Item()).QuantityOnHand);
        Assert.Single((await Snapshot()).GetProperty("movements").EnumerateArray());
    }

    [Fact]
    public async Task Editable_employee_number_is_unique_and_profile_company_changes_persist()
    {
        await AddEmployee("NUMBER-A", new(2026, 1, 1));
        await AddEmployee("NUMBER-B", new(2026, 1, 1));
        var employee = (await Client.GetFromJsonAsync<EmployeePage>("/api/employees?query=NUMBER-A"))!.Items.Single();
        var input = new UpdateEmployeeRequest { EmployeeNumber = " number-c ", FullName = employee.FullName, DepartmentId = employee.DepartmentId, EnrollmentDate = employee.EnrollmentDate, RowVersion = employee.RowVersion };
        var update = await Client.PutAsJsonAsync($"/api/employees/{employee.Id}", input);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var fresh = (await update.Content.ReadFromJsonAsync<EmployeeResponse>())!;
        Assert.Equal("NUMBER-C", fresh.EmployeeNumber);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"/api/employees/{fresh.Id}", new UpdateEmployeeRequest { EmployeeNumber = "NUMBER-B", FullName = fresh.FullName, DepartmentId = fresh.DepartmentId, EnrollmentDate = fresh.EnrollmentDate, RowVersion = fresh.RowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync("/api/auth/profile", new { fullName = "New Name", email = "new-admin@example.test" })).StatusCode);
        var me = await Client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("New Name", me.GetProperty("fullName").GetString());
        Assert.Equal("new-admin@example.test", me.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.PutAsJsonAsync("/api/auth/profile", new { fullName = "Wrong", email = "viewer@example.test" })).StatusCode);
        Assert.Equal("New Name", (await Client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("fullName").GetString());
        var settings = (await Client.GetFromJsonAsync<OrganizationPreferencesResponse>("/api/settings"))!;
        Assert.Equal(HttpStatusCode.OK, (await Client.PutAsJsonAsync("/api/settings", new OrganizationPreferencesInput { CompanyName = "Test Company", TimeZoneId = settings.TimeZoneId, RowVersion = settings.RowVersion })).StatusCode);
        Assert.Equal("Test Company", (await Client.GetFromJsonAsync<OrganizationPreferencesResponse>("/api/settings"))!.CompanyName);
    }
}
