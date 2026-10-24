using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class payrol_carrier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeductionCarrier",
                table: "Payrolls",
                type: "tinyint(1)",
                nullable: false,
                // Existing payroll rows were all produced by runs that deducted, so they backfill as true.
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "PayrollDeductionClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollBatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDeductionClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollDeductionClaims_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductionClaims_EmployeeId_PeriodStart",
                table: "PayrollDeductionClaims",
                columns: new[] { "EmployeeId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductionClaims_PayrollBatchId",
                table: "PayrollDeductionClaims",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductionClaims_TenantId_DeletedAt",
                table: "PayrollDeductionClaims",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollDeductionClaims");

            migrationBuilder.DropColumn(
                name: "IsDeductionCarrier",
                table: "Payrolls");
        }
    }
}
