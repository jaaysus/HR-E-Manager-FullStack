using HrETracker.Data;
using HrETracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace HrETracker.Services;

public class UserManagementService(UserManager<ApplicationUser> userManager, HrETrackerDbContext db) : IUserManagementService
{
    public async Task<IReadOnlyList<UserResponse>> GetAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking().OrderBy(user => user.Email).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var ids = users.Select(user => user.Id).ToArray();
        var roles = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where ids.Contains(userRole.UserId)
                           select new { userRole.UserId, role.Name }).ToListAsync(cancellationToken);
        return users.Select(user => new UserResponse(user.Id, user.Email ?? string.Empty, user.FullName,
            user.IsActive, roles.Where(role => role.UserId == user.Id).Select(role => role.Name!).ToArray())).ToArray();
    }

    public async Task<UserResponse?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(id);
        return user is null ? null : await ToResponseAsync(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var roles = ValidateRoles(request.Roles);
        var name = ValidateName(request.FullName);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), FullName = name };
        EnsureSucceeded(await userManager.CreateAsync(user, request.Password));
        EnsureSucceeded(await userManager.AddToRolesAsync(user, roles));
        await transaction.CommitAsync(cancellationToken);
        return await ToResponseAsync(user);
    }

    public async Task<UserResponse> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var roles = ValidateRoles(request.Roles);
        var name = ValidateName(request.FullName);
        // Serialize changes so two administrators cannot both remove the last active admin.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var user = await userManager.FindByIdAsync(id)
            ?? throw new UserManagementException(404, "User does not exist.");
        var previousRoles = await userManager.GetRolesAsync(user);
        if (user.IsActive && previousRoles.Contains(AccessPolicies.AdministratorRole)
            && (!request.IsActive || !roles.Contains(AccessPolicies.AdministratorRole)))
        {
            var activeAdmins = await (from candidate in db.Users
                                      join userRole in db.UserRoles on candidate.Id equals userRole.UserId
                                      join role in db.Roles on userRole.RoleId equals role.Id
                                      where candidate.IsActive && role.Name == AccessPolicies.AdministratorRole
                                      select candidate.Id).CountAsync(cancellationToken);
            if (activeAdmins <= 1)
                throw new UserManagementException(409, "The last active administrator cannot be disabled or lose the administrator role.");
        }

        user.FullName = name;
        user.IsActive = request.IsActive;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, previousRoles.Except(roles)));
        EnsureSucceeded(await userManager.AddToRolesAsync(user, roles.Except(previousRoles)));
        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));
        await transaction.CommitAsync(cancellationToken);
        return await ToResponseAsync(user);
    }

    private async Task<UserResponse> ToResponseAsync(ApplicationUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, user.IsActive, (await userManager.GetRolesAsync(user)).ToArray());

    private static string ValidateName(string name) => !string.IsNullOrWhiteSpace(name)
        ? name.Trim() : throw new UserManagementException(400, "Full name is required.");

    private static string[] ValidateRoles(string[] roles)
    {
        if (roles.Length == 0 || roles.Any(role => !AccessPolicies.Roles.Contains(role)))
            throw new UserManagementException(400, $"Select at least one valid role: {string.Join(", ", AccessPolicies.Roles)}.");
        return roles.Distinct().ToArray();
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new UserManagementException(result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName" or "ConcurrencyFailure") ? 409 : 400,
                string.Join("; ", result.Errors.Select(error => error.Description)));
    }
}
