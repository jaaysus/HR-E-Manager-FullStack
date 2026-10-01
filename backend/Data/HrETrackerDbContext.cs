using HrETracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Data;

public class HrETrackerDbContext(DbContextOptions<HrETrackerDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<CoatRequest> CoatRequests => Set<CoatRequest>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(employee => employee.Id);
            entity.HasIndex(employee => employee.EmployeeNumber).IsUnique();
            entity.Property(employee => employee.EmployeeNumber).HasMaxLength(50).IsRequired();
            entity.Property(employee => employee.FullName).HasMaxLength(200).IsRequired();
            entity.Property(employee => employee.Department).HasMaxLength(100).IsRequired();
            entity.Property(employee => employee.JobTitle).HasMaxLength(150);
            entity.Property(employee => employee.Notes).HasMaxLength(2000);
            entity.Property(employee => employee.RowVersion).IsRowVersion();
        });

        builder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Sku).IsUnique();
            entity.Property(item => item.Sku).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Department).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Season).HasMaxLength(30).IsRequired();
            entity.HasOne(item => item.Balance).WithOne(balance => balance.InventoryItem)
                .HasForeignKey<InventoryBalance>(balance => balance.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(DemoInventory.Items);
        });
        builder.Entity<InventoryBalance>(entity =>
        {
            entity.ToTable("InventoryBalances");
            entity.HasKey(balance => balance.InventoryItemId);
            entity.Property(balance => balance.RowVersion).IsRowVersion();
        });
        builder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements");
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.Note).HasMaxLength(500).IsRequired();
            entity.HasIndex(movement => new { movement.InventoryItemId, movement.OccurredAtUtc });
            entity.HasIndex(movement => movement.CoatRequestId).IsUnique().HasFilter("[CoatRequestId] IS NOT NULL");
        });
        builder.Entity<CoatRequest>(entity =>
        {
            entity.ToTable("CoatRequests");
            entity.HasKey(request => request.Id);
            entity.HasIndex(request => new { request.EmployeeId, request.CycleNumber }).IsUnique();
            entity.Property(request => request.RowVersion).IsRowVersion();
            entity.Property(request => request.Notes).HasMaxLength(1000);
            entity.HasOne(request => request.Employee).WithMany().HasForeignKey(request => request.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.InventoryItem).WithMany().HasForeignKey(request => request.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
