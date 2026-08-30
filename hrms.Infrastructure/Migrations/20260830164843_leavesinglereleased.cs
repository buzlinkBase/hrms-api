using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class leavesinglereleased : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "TaxContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "SSSContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "PHICContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "CompanyFundedLeavePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "GovernmentFundedLeavePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CompanyAmount",
                table: "leaveApplications",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GovernmentAmount",
                table: "leaveApplications",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayoutMode",
                table: "leaveApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReleasePayrollDate",
                table: "leaveApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "HDMFContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_TaxContributions_EmployeeId_PayrollDate",
                table: "TaxContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxContributions_PayrollBatchId",
                table: "TaxContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SSSContributions_EmployeeId_PayrollDate",
                table: "SSSContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SSSContributions_PayrollBatchId",
                table: "SSSContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PHICContributions_EmployeeId_PayrollDate",
                table: "PHICContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PHICContributions_PayrollBatchId",
                table: "PHICContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_HDMFContributions_EmployeeId_PayrollDate",
                table: "HDMFContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HDMFContributions_PayrollBatchId",
                table: "HDMFContributions",
                column: "PayrollBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaxContributions_EmployeeId_PayrollDate",
                table: "TaxContributions");

            migrationBuilder.DropIndex(
                name: "IX_TaxContributions_PayrollBatchId",
                table: "TaxContributions");

            migrationBuilder.DropIndex(
                name: "IX_SSSContributions_EmployeeId_PayrollDate",
                table: "SSSContributions");

            migrationBuilder.DropIndex(
                name: "IX_SSSContributions_PayrollBatchId",
                table: "SSSContributions");

            migrationBuilder.DropIndex(
                name: "IX_PHICContributions_EmployeeId_PayrollDate",
                table: "PHICContributions");

            migrationBuilder.DropIndex(
                name: "IX_PHICContributions_PayrollBatchId",
                table: "PHICContributions");

            migrationBuilder.DropIndex(
                name: "IX_HDMFContributions_EmployeeId_PayrollDate",
                table: "HDMFContributions");

            migrationBuilder.DropIndex(
                name: "IX_HDMFContributions_PayrollBatchId",
                table: "HDMFContributions");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "TaxContributions");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "SSSContributions");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "PHICContributions");

            migrationBuilder.DropColumn(
                name: "CompanyFundedLeavePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "GovernmentFundedLeavePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "CompanyAmount",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "GovernmentAmount",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "PayoutMode",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "ReleasePayrollDate",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "HDMFContributions");
        }
    }
}
