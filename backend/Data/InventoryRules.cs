using HrETracker.Models;
namespace HrETracker.Data;
public static class InventoryRules
{
    public static readonly DepartmentItemRule[] Items = Build();
    private static DepartmentItemRule[] Build()
    {
        var mappings = new[] { (0, "AllSeason", 3), (1, "AllSeason", 2), (2, "Winter", 0), (2, "Summer", 1), (3, "AllSeason", 6), (4, "Winter", 4), (4, "Summer", 5), (5, "AllSeason", 3) };
        return mappings.Select((m, i) => new DepartmentItemRule { Id = Guid.Parse($"20000000-0000-0000-0000-{i+1:000000000000}"), DepartmentId = DemoDepartments.Items[m.Item1].Id, Season = m.Item2, InventoryItemId = DemoInventory.Items[m.Item3].Id }).ToArray();
    }
}
