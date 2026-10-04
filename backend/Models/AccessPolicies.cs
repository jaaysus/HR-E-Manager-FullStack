namespace HrETracker.Models;

public static class AccessPolicies
{
    public const string AdministratorRole = "HrAdministrator";
    public static readonly string[] Roles = [AdministratorRole, "InventoryManager", "Viewer", "ClothingManager", "ClothingViewer"];
    // InventoryManager remains a supported clothing operator for existing accounts.
    private static readonly string[] ClothingReaders = ["InventoryManager", "ClothingManager", "ClothingViewer"];
    private static readonly string[] ClothingOperators = ["InventoryManager", "ClothingManager"];

    // Grants are explicit: adding a module never grants access to existing roles implicitly.
    public static readonly IReadOnlyDictionary<string, string[]> Permissions = new Dictionary<string, string[]>
    {
        ["platform.users.manage"] = [AdministratorRole],
        ["platform.departments.read"] = Roles,
        ["platform.departments.manage"] = [AdministratorRole],
        ["platform.notifications.read"] = Roles,
        ["platform.settings.read"] = Roles,
        ["platform.settings.manage"] = [AdministratorRole],
        ["employees.read"] = Roles,
        ["employees.manage"] = [AdministratorRole, "ClothingManager"],
        ["employees.import"] = [AdministratorRole, "ClothingManager"],
        ["inventory.read"] = ClothingReaders,
        ["inventory.manage"] = ClothingOperators,
        ["inventory.rules.manage"] = ["ClothingManager"],
        ["coats.read"] = ClothingReaders,
        ["coats.provide"] = ClothingOperators,
        ["coats.manage"] = ["ClothingManager"],
        ["coats.dashboard.read"] = ClothingReaders,
        ["clothing.settings.manage"] = ["ClothingManager"]
    };

    public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles) =>
        Permissions.Where(grant => grant.Value.Intersect(roles, StringComparer.Ordinal).Any())
            .Select(grant => grant.Key).Order(StringComparer.Ordinal).ToArray();
}
