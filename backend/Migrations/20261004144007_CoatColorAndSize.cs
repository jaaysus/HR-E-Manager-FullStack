using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class CoatColorAndSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "InventoryItems",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "InventoryItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000101"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000102"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000103"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000104"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000105"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000106"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });

            migrationBuilder.UpdateData(
                table: "InventoryItems",
                keyColumn: "Id",
                keyValue: new Guid("c42a7c65-6f0e-45f9-b01d-2c2b43000107"),
                columns: new[] { "Color", "Size" },
                values: new object[] { "", "" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "InventoryItems");
        }
    }
}
