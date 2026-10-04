using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsAndEmployeeLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Employees",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "CreatedByUserId", "IsActive", "Name", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "OPERATIONS", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Operations", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "WAREHOUSE", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Warehouse", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "PRODUCTION", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Production", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "QUALITY_CONTROL", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Quality Control", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "MAINTENANCE", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Maintenance", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "LOGISTICS", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Logistics", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            // Preserve existing employees, including departments outside the demo catalog.
            migrationBuilder.Sql("""
                INSERT INTO Departments (Id, Code, Name, IsActive, CreatedAtUtc, UpdatedAtUtc)
                SELECT NEWID(), 'LEGACY_' + REPLACE(CONVERT(varchar(36), NEWID()), '-', ''), names.Name, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
                FROM (SELECT DISTINCT LTRIM(RTRIM(Department)) AS Name FROM Employees) names
                WHERE NOT EXISTS (SELECT 1 FROM Departments d WHERE d.Name = names.Name);
                UPDATE e SET DepartmentId = d.Id
                FROM Employees e JOIN Departments d ON d.Name = LTRIM(RTRIM(e.Department));
                """);
            migrationBuilder.DropColumn(name: "Department", table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId_IsActive",
                table: "Employees",
                columns: new[] { "DepartmentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FullName",
                table: "Employees",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EntityType_EntityId_OccurredAtUtc",
                table: "AuditEvents",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                table: "Employees",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Department", table: "Employees",
                type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "");
            migrationBuilder.Sql("EXEC(N'UPDATE e SET Department = d.Name FROM Employees e JOIN Departments d ON d.Id = e.DepartmentId;');");
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Departments_DepartmentId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId_IsActive",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_FullName",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "Employees");

        }
    }
}
