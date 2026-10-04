using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrETracker.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardReportingAndEmployeeImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CommittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommittedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    ImportedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnrollmentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportRows_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_Type_OccurredAtUtc",
                table: "InventoryMovements",
                columns: new[] { "Type", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_CreatedAtUtc",
                table: "ImportBatches",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_DepartmentId",
                table: "ImportRows",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_EmployeeId",
                table: "ImportRows",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_ImportBatchId_RowNumber",
                table: "ImportRows",
                columns: new[] { "ImportBatchId", "RowNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportRows");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_Type_OccurredAtUtc",
                table: "InventoryMovements");
        }
    }
}
