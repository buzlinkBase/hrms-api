using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class specialPaysettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsEligibleForHolidayPay",
                table: "EmployeeSettings",
                newName: "IsEligibleForSpecialHolidayPay");

            migrationBuilder.AddColumn<bool>(
                name: "IsEligibleForRegularHolidayPay",
                table: "EmployeeSettings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEligibleForRegularHolidayPay",
                table: "EmployeeSettings");

            migrationBuilder.RenameColumn(
                name: "IsEligibleForSpecialHolidayPay",
                table: "EmployeeSettings",
                newName: "IsEligibleForHolidayPay");
        }
    }
}
