using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class RestoreDemoProgramSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "OrganizationSettings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "OrganizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CompanyName", "LowStockThreshold" },
                values: new object[] { "Lear Corporation", 15 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "OrganizationSettings");

            migrationBuilder.UpdateData(
                table: "OrganizationSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "LowStockThreshold",
                value: 5);
        }
    }
}
