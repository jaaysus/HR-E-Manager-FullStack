using HrETracker.Models;

namespace HrETracker.Data;

public static class DemoInventory
{
    public static readonly InventoryItem[] Items =
    [
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000101"), Sku = "production-winter", Name = "Production Coat", Department = "Production", Season = "Winter" },
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000102"), Sku = "production-summer", Name = "Production Coat", Department = "Production", Season = "Summer" },
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000103"), Sku = "warehouse-hi-vis", Name = "Hi-Visibility Coat", Department = "Warehouse", Season = "All-season" },
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000104"), Sku = "logistics-summer", Name = "Logistics Coat", Department = "Logistics", Season = "Summer" },
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000105"), Sku = "engineering-winter", Name = "Engineering Coat", Department = "Maintenance", Season = "Winter" },
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000106"), Sku = "engineering-summer", Name = "Engineering Coat", Department = "Maintenance", Season = "Summer" },
        new() { Id = Guid.Parse("c42a7c65-6f0e-45f9-b01d-2c2b43000107"), Sku = "quality-winter", Name = "Quality Coat", Department = "Quality Control", Season = "Winter" }
    ];
}
