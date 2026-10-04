using HrETracker.Data;
using HrETracker.Models;
using HrETracker.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HrETracker.Api.Tests;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public const string Password = "Test-only-password42!";
    public const string SigningKey = "test-only-signing-key-with-at-least-sixty-four-characters-for-tests";

    private readonly bool useSqlServer;
    private bool sqlDatabaseDeleted;
    private readonly bool fixedBusinessDate;
    private readonly string sqlConnection;
    public AuthApiFactory(bool useSqlServer = false, bool fixedBusinessDate = false)
    {
        this.useSqlServer = useSqlServer;
        this.fixedBusinessDate = fixedBusinessDate;
        sqlConnection = $"Server=.\\SQLEXPRESS;Database=HrETrackerWorkflowTests_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True";
        connection.Open();
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-HR-Auth", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:SigningKey"] = SigningKey,
            ["InitialAdmin:Email"] = "admin@example.test",
            ["Cors:AllowedOrigins:0"] = "http://localhost:8443",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<HrETrackerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<HrETrackerDbContext>>();
            services.AddDbContext<HrETrackerDbContext>(options =>
            {
                if (useSqlServer) options.UseSqlServer(sqlConnection);
                else options.UseSqlite(connection);
            });
            if (fixedBusinessDate)
            {
                services.RemoveAll<IRequestCycleService>();
                services.AddScoped<IRequestCycleService>(sp => new RequestCycleService(sp.GetRequiredService<HrETrackerDbContext>(), sp.GetRequiredService<StockWorkflow>(), new WorkflowTestClock()));
            }
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                && (descriptor.ImplementationType == typeof(IdentityBootstrapService)
                    || descriptor.ImplementationType == typeof(RequestCycleWorker))).ToArray())
                services.Remove(descriptor);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>();
        if (useSqlServer) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        if (!useSqlServer)
        {
            // SQLite has no generated rowversion for seeded departments.
            foreach (var department in await db.Departments.ToListAsync()) department.RowVersion = Guid.NewGuid().ToByteArray();
            await db.SaveChangesAsync();
        }
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AccessPolicies.Roles) Ensure(await roles.CreateAsync(new IdentityRole(role)));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (email, role) in new[] { ("admin@example.test", "HrAdministrator"), ("hradmin@example.test", "HrAdministrator"), ("viewer@example.test", "Viewer"), ("inventory@example.test", "InventoryManager"), ("clothing@example.test", "ClothingManager"), ("clothingviewer@example.test", "ClothingViewer") })
        {
            var user = new ApplicationUser { UserName = email, Email = email, FullName = role };
            Ensure(await users.CreateAsync(user, Password));
            Ensure(await users.AddToRoleAsync(user, role));
            if (email == "admin@example.test") Ensure(await users.AddToRoleAsync(user, "ClothingManager"));
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Description)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && useSqlServer && !sqlDatabaseDeleted)
        {
            sqlDatabaseDeleted = true;
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<HrETrackerDbContext>().Database.EnsureDeleted();
        }
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

public class WorkflowTestClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
}
