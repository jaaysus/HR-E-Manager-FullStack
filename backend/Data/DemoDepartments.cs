using HrETracker.Models;

namespace HrETracker.Data;

public static class DemoDepartments
{
    public static readonly Department[] Items = new[] { "Operations", "Warehouse", "Production", "Quality Control", "Maintenance", "Logistics" }
        .Select((name, index) => new Department
        {
            Id = Guid.Parse($"10000000-0000-0000-0000-{index + 1:000000000000}"),
            Code = name.ToUpperInvariant().Replace(' ', '_'), Name = name,
            CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }).ToArray();
}
