using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAndCoatRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Season = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CoatRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleNumber = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProvidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoatRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoatRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoatRequests_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBalances",
                columns: table => new
                {
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBalances", x => x.InventoryItemId);
                    table.ForeignKey(
                        name: "FK_InventoryBalances_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoatRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "InventoryItems",
                columns: new[] { "Id", "Department", "IsActive", "Name", "Season", "Sku" },
                values: new object[,]
                {
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000101"), "Production", true, "Production Coat", "Winter", "production-winter" },
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000102"), "Production", true, "Production Coat", "Summer", "production-summer" },
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000103"), "Warehouse", true, "Hi-Visibility Coat", "All-season", "warehouse-hi-vis" },
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000104"), "Logistics", true, "Logistics Coat", "Summer", "logistics-summer" },
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000105"), "Maintenance", true, "Engineering Coat", "Winter", "engineering-winter" },
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000106"), "Maintenance", true, "Engineering Coat", "Summer", "engineering-summer" },
                    { new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000107"), "Quality Control", true, "Quality Coat", "Winter", "quality-winter" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoatRequests_EmployeeId_CycleNumber",
                table: "CoatRequests",
                columns: new[] { "EmployeeId", "CycleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoatRequests_InventoryItemId",
                table: "CoatRequests",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Sku",
                table: "InventoryItems",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CoatRequestId",
                table: "InventoryMovements",
                column: "CoatRequestId",
                unique: true,
                filter: "[CoatRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_InventoryItemId_OccurredAtUtc",
                table: "InventoryMovements",
                columns: new[] { "InventoryItemId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoatRequests");

            migrationBuilder.DropTable(
                name: "InventoryBalances");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "InventoryItems");
        }
    }
}
