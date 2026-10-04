using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class CompleteInventoryAndRequestWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "InventoryMovements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "InventoryMovements",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "CoatRequests",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "CoatRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvidedByUserId",
                table: "CoatRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "CoatRequests",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "CoatRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("UPDATE CoatRequests SET UpdatedAtUtc = RequestedAtUtc WHERE UpdatedAtUtc = '0001-01-01';");

            migrationBuilder.CreateTable(
                name: "DepartmentItemRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentItemRules", x => x.Id);
                    table.CheckConstraint("CK_Rule_Dates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.ForeignKey(
                        name: "FK_DepartmentItemRules_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepartmentItemRules_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LowStockAlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LowStockThreshold = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSettings", x => x.Id);
                    table.CheckConstraint("CK_Settings_Singleton", "[Id] = 1");
                    table.CheckConstraint("CK_Settings_Threshold", "[LowStockThreshold] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "NotificationReads",
                columns: table => new
                {
                    NotificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReads", x => new { x.NotificationId, x.UserId });
                    table.ForeignKey(
                        name: "FK_NotificationReads_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificationReads_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "DepartmentItemRules",
                columns: new[] { "Id", "CreatedAtUtc", "CreatedByUserId", "DepartmentId", "EffectiveFrom", "EffectiveTo", "InventoryItemId", "Season", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000001"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000104"), "AllSeason", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000002"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000103"), "AllSeason", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000003"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000101"), "Winter", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000003"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000102"), "Summer", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000004"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000107"), "AllSeason", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000005"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000105"), "Winter", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000005"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000106"), "Summer", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("10000000-0000-0000-0000-000000000006"), new DateOnly(2024, 1, 1), null, new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000104"), "AllSeason", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null }
                });

            // Preserve balances recorded before this migration; initialize only missing variants.
            migrationBuilder.Sql("INSERT INTO InventoryBalances (InventoryItemId, QuantityOnHand) SELECT Id, 0 FROM InventoryItems i WHERE NOT EXISTS (SELECT 1 FROM InventoryBalances b WHERE b.InventoryItemId = i.Id);");

            migrationBuilder.InsertData(
                table: "OrganizationSettings",
                columns: new[] { "Id", "LowStockAlertsEnabled", "LowStockThreshold", "TimeZoneId", "UpdatedAtUtc", "UpdatedByUserId" },
                values: new object[] { 1, true, 5, "Africa/Casablanca", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Movement_Quantity",
                table: "InventoryMovements",
                sql: "[Quantity] <> 0 AND ([Type] <> 0 OR [Quantity] > 0) AND ([Type] <> 1 OR [Quantity] = -1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Balance_Nonnegative",
                table: "InventoryBalances",
                sql: "[QuantityOnHand] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentItemRules_DepartmentId_Season_EffectiveFrom",
                table: "DepartmentItemRules",
                columns: new[] { "DepartmentId", "Season", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentItemRules_InventoryItemId",
                table: "DepartmentItemRules",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReads_UserId",
                table: "NotificationReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_DeduplicationKey",
                table: "Notifications",
                column: "DeduplicationKey",
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_CoatRequests_CoatRequestId",
                table: "InventoryMovements",
                column: "CoatRequestId",
                principalTable: "CoatRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_CoatRequests_CoatRequestId",
                table: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "DepartmentItemRules");

            migrationBuilder.DropTable(
                name: "NotificationReads");

            migrationBuilder.DropTable(
                name: "OrganizationSettings");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Movement_Quantity",
                table: "InventoryMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Balance_Nonnegative",
                table: "InventoryBalances");


            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "CoatRequests");

            migrationBuilder.DropColumn(
                name: "ProvidedByUserId",
                table: "CoatRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "CoatRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "CoatRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "InventoryItemId",
                table: "CoatRequests",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
