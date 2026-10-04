using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using HrETracker.Data;
using HrETracker.Models;
using HrETracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HrETracker.Api.Tests;

public sealed class DashboardImportTests : IAsyncLifetime
{
    private readonly AuthApiFactory factory = new(fixedBusinessDate: true);
    private HttpClient client = null!;
    private const string Header = "Employee ID,Full Name,Department,Enrollment Date,Job Title,Notes\r\n";
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
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
    }
    private Task<HttpResponseMessage> Upload(byte[] data, string fileName = "employees.csv")
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(data), "file", fileName);
        return client.PostAsync("/api/employee-imports", form);
    }
    private async Task<ImportSummary> Stage(string csv)
    {
        var response = await Upload(Encoding.UTF8.GetBytes(csv));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ImportSummary>())!;
    }

    [Fact]
    public async Task Csv_preview_commit_retry_and_history_are_persisted_and_redacted()
    {
        var batch = await Stage(Header + " emp-1 ,\"Name, With Comma\",Operations,2026-01-01, Supervisor ,\"private\r\nnotes\"\r\nEMP-2,Second,Warehouse,2026-02-01,,\r\n");
        Assert.Equal("Validated", batch.Status); Assert.Equal(2, batch.RowCount); Assert.Equal(64, batch.Sha256.Length);
        var preview = (await client.GetFromJsonAsync<ImportPreview>($"/api/employee-imports/{batch.Id}?pageSize=1"))!;
        var row = Assert.Single(preview.Rows);
        Assert.Equal("EMP-1", row.EmployeeNumber); Assert.Equal("Name, With Comma", row.FullName);
        Assert.Equal("private\r\nnotes", row.Notes); Assert.Empty(row.Errors);
        Assert.Empty((await client.GetFromJsonAsync<EmployeePage>("/api/employees"))!.Items);
        var committed = await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null);
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        var result = (await committed.Content.ReadFromJsonAsync<ImportSummary>())!;
        Assert.Equal("Committed", result.Status); Assert.Equal(2, result.ImportedCount); Assert.NotNull(result.CommittedByUserId);
        var retry = await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null);
        Assert.Equal(result, await retry.Content.ReadFromJsonAsync<ImportSummary>());
        Assert.Equal(2, (await client.GetFromJsonAsync<EmployeePage>("/api/employees"))!.TotalCount);
        var history = (await client.GetFromJsonAsync<ImportHistory>("/api/employee-imports"))!;
        Assert.Equal(result, Assert.Single(history.Items));
        var fresh = (await client.GetFromJsonAsync<ImportPreview>($"/api/employee-imports/{batch.Id}"))!;
        Assert.All(fresh.Rows, r => Assert.NotNull(r.EmployeeId));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        Assert.Equal(2, await db.ImportRows.CountAsync());
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == "EmployeeImport.Committed"));
        Assert.All(await db.AuditEvents.ToListAsync(), a => { Assert.DoesNotContain("private", a.AfterJson); Assert.DoesNotContain("With Comma", a.AfterJson); });
    }

    [Fact]
    public async Task Invalid_rows_preview_all_errors_and_commit_nothing()
    {
        await client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = "EXISTS", FullName = "Existing", DepartmentId = DemoDepartments.Items[0].Id, EnrollmentDate = new(2026, 1, 1) });
        var batch = await Stage(Header + "exists,Name,Operations,2026-01-01,,\nDUP,Name,Unknown,bad,,\ndup,,Warehouse,2026-01-01,,\nNEW,Name,Operations,2026-01-01,,\n");
        Assert.Equal("Invalid", batch.Status); Assert.Equal(3, batch.ErrorCount);
        var preview = (await client.GetFromJsonAsync<ImportPreview>($"/api/employee-imports/{batch.Id}"))!;
        Assert.Contains(preview.Rows[0].Errors, e => e.Contains("already exists"));
        Assert.True(preview.Rows[1].Errors.Count() >= 3); Assert.Empty(preview.Rows[3].Errors);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<EmployeePage>("/api/employees"))!.TotalCount);
    }

    [Fact]
    public async Task Commit_revalidates_duplicates_and_import_does_not_require_clothing_rules()
    {
        var batch = await Stage(Header + "NEW,Name,Operations,2026-01-01,,\nOTHER,Name,Warehouse,2026-01-01,,\n");
        await client.PostAsJsonAsync("/api/employees", new CreateEmployeeRequest { EmployeeNumber = "NEW", FullName = "Created later", DepartmentId = DemoDepartments.Items[0].Id, EnrollmentDate = new(2026, 1, 1) });
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null)).StatusCode);
        var preview = (await client.GetFromJsonAsync<ImportPreview>($"/api/employee-imports/{batch.Id}"))!;
        Assert.Equal("Invalid", preview.Batch.Status); Assert.NotEmpty(preview.Rows[0].Errors);
        Assert.Equal(1, (await client.GetFromJsonAsync<EmployeePage>("/api/employees"))!.TotalCount);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        db.DepartmentItemRules.RemoveRange(await db.DepartmentItemRules.Where(r => r.DepartmentId == DemoDepartments.Items[1].Id).ToListAsync());
        await db.SaveChangesAsync();
        var noRule = await Stage(Header + "RULE,Name,Warehouse,2026-01-01,,\n");
        Assert.Equal("Validated", noRule.Status);
        Assert.Empty((await client.GetFromJsonAsync<ImportPreview>($"/api/employee-imports/{noRule.Id}"))!.Rows[0].Errors);
    }

    [Fact]
    public async Task Failure_on_later_employee_rolls_back_every_employee_row_outcome_and_commit_audit()
    {
        var batch = await Stage(Header + "FIRST,Name,Operations,2026-01-01,,\nSECOND,Name,Warehouse,2026-01-01,,\n");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectSecond BEFORE INSERT ON Employees WHEN NEW.EmployeeNumber = 'SECOND' BEGIN SELECT RAISE(ABORT, 'Injected import failure'); END;");
        Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsync($"/api/employee-imports/{batch.Id}/commit", null)).StatusCode);
        Assert.Equal(0, await db.Employees.CountAsync());
        Assert.Equal(0, await db.AuditEvents.CountAsync(a => a.Action == "Employee.Created" || a.Action == "EmployeeImport.Committed"));
        Assert.Equal(0, await db.ImportRows.CountAsync(r => r.EmployeeId != null));
        Assert.Equal("Validated", (await db.ImportBatches.SingleAsync()).Status);
    }

    [Fact]
    public async Task Excel_dates_in_both_date_systems_are_normalized()
    {
        foreach (var use1904 in new[] { false, true })
        {
            var response = await Upload(Workbook(use1904), "employees.xlsx");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var batch = (await response.Content.ReadFromJsonAsync<ImportSummary>())!;
            Assert.Equal("Validated", batch.Status);
            var row = Assert.Single((await client.GetFromJsonAsync<ImportPreview>($"/api/employee-imports/{batch.Id}"))!.Rows);
            Assert.Equal(new DateOnly(2026, 1, 1), row.EnrollmentDate);
        }
    }

    [Fact]
    public async Task Malformed_unsupported_and_oversized_files_are_rejected_without_batches()
    {
        foreach (var (data, name) in new[] {
            (Encoding.UTF8.GetBytes(Header + "1,\"unclosed"), "bad.csv"),
            (Encoding.UTF8.GetBytes("Employee ID,Full Name\n1,Name"), "bad.csv"),
            (Encoding.UTF8.GetBytes(Header + "1,Name,Operations,2026-01-01,,"), "bad.xlsx"),
            (Array.Empty<byte>(), "empty.csv"), (new byte[ImportFileParser.MaxBytes + 1], "large.csv"),
            (Encoding.UTF8.GetBytes(Header), "bad.exe") })
            Assert.Equal(HttpStatusCode.BadRequest, (await Upload(data, name)).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<ImportHistory>("/api/employee-imports"))!.Items);
    }

    [Fact]
    public async Task Import_is_write_authorized_and_dashboard_requires_a_clothing_role()
    {
        await Login("viewer@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/employee-imports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(Encoding.UTF8.GetBytes(Header))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/employee-imports/{Guid.NewGuid()}/commit", null)).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_uses_ledger_business_months_persisted_counts_and_per_user_read_state()
    {
        var empty = (await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard"))!;
        Assert.Equal(0, empty.ActiveEmployees); Assert.Equal(0, empty.QuantityOnHand);
        Assert.Equal(7, empty.LowStockItems); Assert.Equal(12, empty.MonthlyIssues.Count);
        Assert.All(empty.MonthlyIssues, m => Assert.Equal(0, m.Quantity));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
            var department = DemoDepartments.Items[0].Id;
            var employee = new Employee { EmployeeNumber = "DASH", FullName = "Name", DepartmentId = department, EnrollmentDate = new(2025, 1, 1) };
            db.Employees.Add(employee);
            db.Employees.Add(new Employee { EmployeeNumber = "INACTIVE", FullName = "Name", DepartmentId = department, EnrollmentDate = new(2025, 1, 1), IsActive = false });
            foreach (var status in Enum.GetValues<CoatRequestStatus>()) db.CoatRequests.Add(new CoatRequest { EmployeeId = employee.Id, CycleNumber = (int)status + 1, DueDate = new(2026, 1, 1), Status = status });
            // Fixed UTC+01 makes 23:00 UTC the start of the next business month.
            (await db.OrganizationSettings.SingleAsync()).TimeZoneId = "Etc/GMT-1";
            var item = DemoInventory.Items[0].Id;
            (await db.InventoryBalances.SingleAsync(b => b.InventoryItemId == item)).QuantityOnHand = 12;
            foreach (var time in new[] { new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 31, 22, 59, 59, DateTimeKind.Utc), new DateTime(2026, 1, 31, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 31, 23, 0, 0, DateTimeKind.Utc) })
                db.InventoryMovements.Add(new InventoryMovement { InventoryItemId = item, Type = InventoryMovementType.Allocation, Quantity = -1, Note = "Issue", OccurredAtUtc = time });
            db.InventoryMovements.Add(new InventoryMovement { InventoryItemId = item, Type = InventoryMovementType.Receipt, Quantity = 20, Note = "Receipt", OccurredAtUtc = new(2026, 1, 1) });
            db.Notifications.Add(new Notification { Type = "Info", Message = "Public", RelatedEntityType = "Employee", CreatedAtUtc = DateTime.UtcNow });
            db.Notifications.Add(new Notification { Type = "Info", Message = "Private", RelatedEntityType = "Employee", RecipientUserId = "another-user", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var dashboard = (await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard?year=2026"))!;
        Assert.Equal(1, dashboard.ActiveEmployees); Assert.Equal(1, dashboard.InactiveEmployees);
        Assert.Equal(1, dashboard.PendingRequests); Assert.Equal(1, dashboard.OutOfStockRequests);
        Assert.Equal(1, dashboard.ProvidedRequests); Assert.Equal(1, dashboard.CancelledRequests);
        Assert.Equal(12, dashboard.QuantityOnHand); Assert.Equal(7, dashboard.LowStockItems);
        Assert.Equal(2, dashboard.MonthlyIssues[0].Quantity); Assert.Equal(1, dashboard.MonthlyIssues[1].Quantity);
        Assert.Equal(3, dashboard.MonthlyIssues.Sum(m => m.Quantity)); Assert.Equal(1, dashboard.UnreadNotifications);
        await client.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(0, (await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard"))!.UnreadNotifications);
        await Login("clothingviewer@example.test");
        Assert.Equal(1, (await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard"))!.UnreadNotifications);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/dashboard?year=9999")).StatusCode);
    }

    internal static byte[] Workbook(bool use1904)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Add(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(xml); }
            Add("[Content_Types].xml", """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>
                """);
            Add("_rels/.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>
                """);
            Add("xl/workbook.xml", $"<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><workbookPr date1904=\"{(use1904 ? 1 : 0)}\"/><sheets><sheet name=\"Employees\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
                """);
            Add("xl/styles.xml", """
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><cellXfs count="2"><xf numFmtId="0"/><xf numFmtId="14" applyNumberFormat="1"/></cellXfs></styleSheet>
                """);
            var serial = new DateTime(2026, 1, 1).ToOADate() - (use1904 ? 1462 : 0);
            Add("xl/worksheets/sheet1.xml", $"<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Employee ID</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Full Name</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>Department</t></is></c><c r=\"D1\" t=\"inlineStr\"><is><t>Enrollment Date</t></is></c></row><row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>EXCEL</t></is></c><c r=\"B2\" t=\"inlineStr\"><is><t>Excel Name</t></is></c><c r=\"C2\" t=\"inlineStr\"><is><t>Operations</t></is></c><c r=\"D2\" s=\"1\"><v>{serial}</v></c></row></sheetData></worksheet>");
        }
        return stream.ToArray();
    }
}
