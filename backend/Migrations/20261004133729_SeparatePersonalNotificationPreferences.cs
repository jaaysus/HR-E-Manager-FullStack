using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class SeparatePersonalNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyClothingActivity",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyLowStock",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyClothingActivity",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NotifyLowStock",
                table: "AspNetUsers");
        }
    }
}
