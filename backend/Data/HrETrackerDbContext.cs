using HrETracker.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HrETracker.Data;

public class HrETrackerDbContext(DbContextOptions<HrETrackerDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<CoatRequest> CoatRequests => Set<CoatRequest>();
    public DbSet<DepartmentItemRule> DepartmentItemRules => Set<DepartmentItemRule>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(u => u.NotifyLowStock).HasDefaultValue(true);
        builder.Entity<ApplicationUser>().Property(u => u.NotifyClothingActivity).HasDefaultValue(true);
        builder.Entity<ImportBatch>(e =>
        {
            e.Property(b => b.FileName).HasMaxLength(255);
            e.Property(b => b.Sha256).HasMaxLength(64);
            e.Property(b => b.Status).HasMaxLength(30);
            e.HasIndex(b => b.CreatedAtUtc);
            e.HasMany(b => b.Rows).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ImportRow>(e =>
        {
            e.HasIndex(r => new { r.ImportBatchId, r.RowNumber }).IsUnique();
            e.HasOne<Employee>().WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Department>().WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<InventoryMovement>().HasIndex(m => new { m.Type, m.OccurredAtUtc });
        builder.Entity<NotificationRead>(e =>
        {
            e.HasKey(r => new { r.NotificationId, r.UserId });
            e.HasOne<Notification>().WithMany().HasForeignKey(r => r.NotificationId);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<DepartmentItemRule>(e =>
        {
            e.Property(r => r.Season).HasMaxLength(30);
            e.Property(r => r.RowVersion).IsRowVersion();
            e.ToTable("DepartmentItemRules", t => t.HasCheckConstraint("CK_Rule_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"));
            e.HasIndex(r => new { r.DepartmentId, r.Season, r.EffectiveFrom }).IsUnique();
            e.HasOne<Department>().WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<InventoryItem>().WithMany().HasForeignKey(r => r.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasData(InventoryRules.Items);
        });
        builder.Entity<OrganizationSettings>(e =>
        {
            e.ToTable("OrganizationSettings", t => { t.HasCheckConstraint("CK_Settings_Singleton", "[Id] = 1"); t.HasCheckConstraint("CK_Settings_Threshold", "[LowStockThreshold] >= 0"); });
            e.Property(r => r.Id).ValueGeneratedNever();
            e.Property(r => r.TimeZoneId).HasMaxLength(100);
            e.Property(r => r.RowVersion).IsRowVersion();
            e.HasData(new OrganizationSettings { RowVersion = new byte[] { 1 }, UpdatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        });
        builder.Entity<Notification>(e =>
        {
            e.Property(n => n.Type).HasMaxLength(50);
            e.Property(n => n.Message).HasMaxLength(1000);
            e.Property(n => n.RelatedEntityType).HasMaxLength(100);
            e.Property(n => n.DeduplicationKey).HasMaxLength(200);
            e.HasIndex(n => n.DeduplicationKey).IsUnique().HasFilter("[DeduplicationKey] IS NOT NULL");
        });
        builder.Entity<Department>(entity =>
        {
            entity.Property(d => d.Code).HasMaxLength(50).IsRequired();
            entity.Property(d => d.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(d => d.Code).IsUnique();
            entity.Property(d => d.RowVersion).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                entity.Property(d => d.RowVersion).ValueGeneratedNever();
            entity.HasData(DemoDepartments.Items);
        });
        builder.Entity<AuditEvent>(entity =>
        {
            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();
            entity.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            entity.Property(a => a.CorrelationId).HasMaxLength(200);
            entity.HasIndex(a => new { a.EntityType, a.EntityId, a.OccurredAtUtc });
        });
        builder.Entity<ApplicationUser>().Property(user => user.IsActive).HasDefaultValue(true);
        builder.Entity<AuthSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.RefreshTokenHash).HasMaxLength(64).IsRequired();
            entity.Property(session => session.SecurityStamp).HasMaxLength(256).IsRequired();
            entity.Property(session => session.Version).IsConcurrencyToken();
            entity.HasIndex(session => session.RefreshTokenHash).IsUnique();
            entity.HasIndex(session => new { session.UserId, session.ExpiresAtUtc });
            entity.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId);
        });
        builder.Entity<Employee>(entity =>
        {
            entity.ToTable("Employees");
            entity.HasKey(employee => employee.Id);
            entity.HasIndex(employee => employee.EmployeeNumber).IsUnique();
            entity.Property(employee => employee.EmployeeNumber).HasMaxLength(50).IsRequired();
            entity.Property(employee => employee.FullName).HasMaxLength(200).IsRequired();
            entity.HasOne(employee => employee.Department).WithMany().HasForeignKey(employee => employee.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(employee => new { employee.DepartmentId, employee.IsActive });
            entity.HasIndex(employee => employee.FullName);
            entity.Property(employee => employee.JobTitle).HasMaxLength(150);
            entity.Property(employee => employee.Notes).HasMaxLength(2000);
            entity.Property(employee => employee.RowVersion).IsRowVersion();
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                entity.Property(employee => employee.RowVersion).ValueGeneratedNever();
        });

        builder.Entity<InventoryItem>(entity =>
        {
            entity.ToTable("InventoryItems");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Sku).IsUnique();
            entity.Property(item => item.Sku).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Color).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Size).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Department).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Season).HasMaxLength(30).IsRequired();
            entity.HasOne(item => item.Balance).WithOne(balance => balance.InventoryItem)
                .HasForeignKey<InventoryBalance>(balance => balance.InventoryItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasData(DemoInventory.Items);
        });
        builder.Entity<InventoryBalance>(entity =>
        {
            entity.ToTable("InventoryBalances", t => t.HasCheckConstraint("CK_Balance_Nonnegative", "[QuantityOnHand] >= 0"));
            entity.HasKey(balance => balance.InventoryItemId);
            entity.Property(balance => balance.RowVersion).IsRowVersion();
            entity.HasData(DemoInventory.Items.Select(i => new InventoryBalance { InventoryItemId = i.Id, RowVersion = new byte[] { 1 } }));
        });
        builder.Entity<InventoryMovement>(entity =>
        {
            entity.ToTable("InventoryMovements", t => t.HasCheckConstraint("CK_Movement_Quantity", "[Quantity] <> 0 AND ([Type] <> 0 OR [Quantity] > 0) AND ([Type] <> 1 OR [Quantity] = -1)"));
            entity.Property(m => m.Reference).HasMaxLength(100);
            entity.HasOne<CoatRequest>().WithMany().HasForeignKey(m => m.CoatRequestId).OnDelete(DeleteBehavior.Restrict);
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
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            builder.Entity<DepartmentItemRule>().Property(e => e.RowVersion).ValueGeneratedNever();
            builder.Entity<InventoryBalance>().Property(e => e.RowVersion).ValueGeneratedNever();
            builder.Entity<CoatRequest>().Property(e => e.RowVersion).ValueGeneratedNever();
            builder.Entity<OrganizationSettings>().Property(e => e.RowVersion).ValueGeneratedNever();
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<AuditEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
        if (ChangeTracker.Entries<InventoryMovement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Stock ledger is append-only.");
        // SQL Server supplies rowversion; SQLite integration tests need an equivalent token.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            foreach (var entry in ChangeTracker.Entries<Employee>().Where(e => e.State is EntityState.Added or EntityState.Modified))
                entry.Property(e => e.RowVersion).CurrentValue = Guid.NewGuid().ToByteArray();
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            foreach (var entry in ChangeTracker.Entries().Where(e => (e.Entity is InventoryBalance or CoatRequest or DepartmentItemRule or HrETracker.Models.OrganizationSettings) && (e.State is EntityState.Added or EntityState.Modified)))
                entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
        return base.SaveChangesAsync(cancellationToken);
    }
}
