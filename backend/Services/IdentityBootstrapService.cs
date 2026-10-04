using HrETracker.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HrETracker.Services;

public class IdentityBootstrapService(IServiceScopeFactory scopeFactory, IConfiguration configuration, IHostEnvironment environment, ILogger<IdentityBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var admin = configuration.GetSection("InitialAdmin").Get<InitialAdminSettings>() ?? new InitialAdminSettings();
            var defaults = new InitialAdminSettings();
            var email = string.IsNullOrWhiteSpace(admin.Email) ? defaults.Email : admin.Email.Trim();
            var password = string.IsNullOrWhiteSpace(admin.Password) ? defaults.Password : admin.Password;
            var fullName = string.IsNullOrWhiteSpace(admin.FullName) ? defaults.FullName : admin.FullName;
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("InitialAdmin:Email must be configured through user secrets or an environment variable.");
            var connectionFingerprint = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(configuration.GetConnectionString("DefaultConnection") ?? string.Empty)));
            var statePath = Path.Combine(environment.ContentRootPath, "identity-bootstrap.json");
            // Role definitions must be reconciled even when administrator bootstrap is cached.
            using var scope = scopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            foreach (var role in AccessPolicies.Roles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!await roleManager.RoleExistsAsync(role))
                    EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
            }
            if (File.Exists(statePath))
            {
                BootstrapState? state = null;
                try
                {
                    state = JsonSerializer.Deserialize<BootstrapState>(
                        await File.ReadAllTextAsync(statePath, cancellationToken));
                }
                catch (JsonException)
                {
                    logger.LogWarning("Administrator bootstrap state is invalid; checking the database again.");
                }

                if (state is { Exists: true }
                    && string.Equals(state.Email, email, StringComparison.OrdinalIgnoreCase)
                    && state.ConnectionFingerprint == connectionFingerprint)
                {
                    logger.LogInformation("Initial administrator {Email} already exists; roles reconciled and administrator lookup skipped.", email);
                    return;
                }
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                if (string.IsNullOrWhiteSpace(password))
                    throw new InvalidOperationException("InitialAdmin:Password must be configured to create the initial administrator.");
                user = new ApplicationUser { UserName = email, Email = email, FullName = fullName };
                EnsureSucceeded(await userManager.CreateAsync(user, password), "create initial administrator");
            }
            if (!user.IsActive)
                throw new InvalidOperationException("The configured initial administrator is disabled. Configure an active administrator.");

            if (!await userManager.IsInRoleAsync(user, "HrAdministrator"))
                EnsureSucceeded(await userManager.AddToRoleAsync(user, "HrAdministrator"), "assign administrator role");

            var temporaryStatePath = statePath + ".tmp";
            await File.WriteAllTextAsync(temporaryStatePath,
                JsonSerializer.Serialize(new BootstrapState(true, email, connectionFingerprint),
                    new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            File.Move(temporaryStatePath, statePath, overwrite: true);

            logger.LogInformation("Initial administrator {Email} is ready.", email);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Required administrator bootstrap failed. Apply database migrations and verify the initial administrator configuration before restarting.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record BootstrapState(bool Exists, string Email, string ConnectionFingerprint);

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}
