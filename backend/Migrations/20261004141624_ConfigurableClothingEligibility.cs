using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableClothingEligibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EligibilityEffectiveAtUtc",
                table: "OrganizationSettings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EligibilityInterval",
                table: "OrganizationSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EligibilityUnit",
                table: "OrganizationSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "OrganizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "EligibilityEffectiveAtUtc", "EligibilityInterval", "EligibilityUnit" },
                values: new object[] { null, 6, "Months" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EligibilityEffectiveAtUtc",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "EligibilityInterval",
                table: "OrganizationSettings");

            migrationBuilder.DropColumn(
                name: "EligibilityUnit",
                table: "OrganizationSettings");
        }
    }
}
