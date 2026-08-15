using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class leaveresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LeaveReset",
                table: "Leaves",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "Less",
                table: "LeaveLedgers",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "LeaveLedgers",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Add",
                table: "LeaveLedgers",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(65,30)");

            migrationBuilder.AddColumn<string>(
                name: "DtrBatchCode",
                table: "LeaveLedgers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EntryType",
                table: "LeaveLedgers",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeaveCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LeaveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Granted = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Used = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveCredits_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveCredits_Leaves_LeaveId",
                        column: x => x.LeaveId,
                        principalTable: "Leaves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_LeaveCreditsId",
                table: "LeaveLedgers",
                column: "LeaveCreditsId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_LeaveId",
                table: "LeaveLedgers",
                column: "LeaveId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_ReferenceApplicationId",
                table: "LeaveLedgers",
                column: "ReferenceApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_EmployeeId_LeaveId_PeriodYear",
                table: "LeaveCredits",
                columns: new[] { "EmployeeId", "LeaveId", "PeriodYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_LeaveId",
                table: "LeaveCredits",
                column: "LeaveId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_TenantId_DeletedAt",
                table: "LeaveCredits",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveLedgers_LeaveCredits_LeaveCreditsId",
                table: "LeaveLedgers",
                column: "LeaveCreditsId",
                principalTable: "LeaveCredits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveLedgers_Leaves_LeaveId",
                table: "LeaveLedgers",
                column: "LeaveId",
                principalTable: "Leaves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveLedgers_leaveApplications_ReferenceApplicationId",
                table: "LeaveLedgers",
                column: "ReferenceApplicationId",
                principalTable: "leaveApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveLedgers_LeaveCredits_LeaveCreditsId",
                table: "LeaveLedgers");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveLedgers_Leaves_LeaveId",
                table: "LeaveLedgers");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveLedgers_leaveApplications_ReferenceApplicationId",
                table: "LeaveLedgers");

            migrationBuilder.DropTable(
                name: "LeaveCredits");

            migrationBuilder.DropIndex(
                name: "IX_LeaveLedgers_LeaveCreditsId",
                table: "LeaveLedgers");

            migrationBuilder.DropIndex(
                name: "IX_LeaveLedgers_LeaveId",
                table: "LeaveLedgers");

            migrationBuilder.DropIndex(
                name: "IX_LeaveLedgers_ReferenceApplicationId",
                table: "LeaveLedgers");

            migrationBuilder.DropColumn(
                name: "DtrBatchCode",
                table: "LeaveLedgers");

            migrationBuilder.DropColumn(
                name: "EntryType",
                table: "LeaveLedgers");

            migrationBuilder.AlterColumn<int>(
                name: "LeaveReset",
                table: "Leaves",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<decimal>(
                name: "Less",
                table: "LeaveLedgers",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "Balance",
                table: "LeaveLedgers",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "Add",
                table: "LeaveLedgers",
                type: "decimal(65,30)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
