using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class lackingfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NDPay",
                table: "Payrolls",
                newName: "TotalLoans");

            migrationBuilder.RenameColumn(
                name: "NDOTPay",
                table: "Payrolls",
                newName: "NightDifferentialPay");

            migrationBuilder.RenameColumn(
                name: "BasicSalary",
                table: "Payrolls",
                newName: "NightDifferentialOTPay");

            migrationBuilder.AddColumn<decimal>(
                name: "BasicPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DailyRate",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DtrBatchCodes",
                table: "Payrolls",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "HolidayPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolidayUnworkedPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SalaryType",
                table: "Payrolls",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasicPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DailyRate",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DtrBatchCodes",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "HolidayPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolidayUnworkedPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SalaryType",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "TotalLoans",
                table: "Payrolls",
                newName: "NDPay");

            migrationBuilder.RenameColumn(
                name: "NightDifferentialPay",
                table: "Payrolls",
                newName: "NDOTPay");

            migrationBuilder.RenameColumn(
                name: "NightDifferentialOTPay",
                table: "Payrolls",
                newName: "BasicSalary");
        }
    }
}
