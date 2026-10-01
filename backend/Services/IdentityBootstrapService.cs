using HrETracker.Models;
using Microsoft.AspNetCore.Identity;

namespace HrETracker.Services;

public class IdentityBootstrapService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<IdentityBootstrapService> logger) : IHostedService
{
    private static readonly string[] Roles = ["HrAdministrator", "InventoryManager", "Viewer"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var role in Roles)
                if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));

            var admin = configuration.GetSection("InitialAdmin").Get<InitialAdminSettings>() ?? new InitialAdminSettings();
            if (string.IsNullOrWhiteSpace(admin.Email) || string.IsNullOrWhiteSpace(admin.Password))
            {
                logger.LogWarning("No initial administrator was configured. Set InitialAdmin__Email, InitialAdmin__Password, and InitialAdmin__FullName before first use.");
                return;
            }
            if (await userManager.FindByEmailAsync(admin.Email) is not null) return;

            var user = new ApplicationUser { UserName = admin.Email, Email = admin.Email, FullName = admin.FullName };
            var result = await userManager.CreateAsync(user, admin.Password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            await userManager.AddToRoleAsync(user, "HrAdministrator");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Identity bootstrap was deferred because the database is unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
