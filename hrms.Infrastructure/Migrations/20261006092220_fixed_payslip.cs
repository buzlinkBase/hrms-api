using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class fixed_payslip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FixedBasicSalary",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsRegularHolidayIncluded",
                table: "Payrolls",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsRestDayPaid",
                table: "Payrolls",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSpecialNonWorkingIncluded",
                table: "Payrolls",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FixedBasicSalary",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "IsRegularHolidayIncluded",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "IsRestDayPaid",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "IsSpecialNonWorkingIncluded",
                table: "Payrolls");
        }
    }
}
