using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class payroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Absences",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "AbsentCount",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "HolidayPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LateHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolNDOTPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolNDPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolNightDiffHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolNightDiffOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolOTPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolidayPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "NightDifferentialHour",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "NightDifferentialPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "OvertimeHour",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNetHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolNightDiffHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolNightDiffOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialWorkDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialWorkDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialWorkDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialWorkDayOTHours",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "UnderTimeHours",
                table: "Payrolls",
                newName: "SpecialPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkingOTPay",
                table: "Payrolls",
                newName: "SpecialOTPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkingNDPay",
                table: "Payrolls",
                newName: "SpecialNDPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkingNDOTPay",
                table: "Payrolls",
                newName: "SpecialNDOTPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkDayPay",
                table: "Payrolls",
                newName: "RestSpecialPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkDayOTPay",
                table: "Payrolls",
                newName: "RestSpecialOTPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkDayNDPay",
                table: "Payrolls",
                newName: "RestSpecialNDPay");

            migrationBuilder.RenameColumn(
                name: "SpecialWorkDayNDOTPay",
                table: "Payrolls",
                newName: "RestSpecialNDOTPay");

            migrationBuilder.RenameColumn(
                name: "SpecialNonWorkingOTPay",
                table: "Payrolls",
                newName: "RestLegalPay");

            migrationBuilder.RenameColumn(
                name: "SpecialNonWorkingNDPay",
                table: "Payrolls",
                newName: "RestLegalOTPay");

            migrationBuilder.RenameColumn(
                name: "SpecialNonWorkingNDOTPay",
                table: "Payrolls",
                newName: "RestLegalNDPay");

            migrationBuilder.RenameColumn(
                name: "SpecialHolidayPay",
                table: "Payrolls",
                newName: "RestLegalNDOTPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialDayPay",
                table: "Payrolls",
                newName: "NDPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialDayOTPay",
                table: "Payrolls",
                newName: "NDOTPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialDayNDPay",
                table: "Payrolls",
                newName: "LegalPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialDayNDOTPay",
                table: "Payrolls",
                newName: "LegalOTPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalDayPay",
                table: "Payrolls",
                newName: "LegalNDPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalDayOTPay",
                table: "Payrolls",
                newName: "LegalNDOTPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalDayNDPay",
                table: "Payrolls",
                newName: "GrossPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalDayNDOTPay",
                table: "Payrolls",
                newName: "AbsencesAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SpecialPay",
                table: "Payrolls",
                newName: "UnderTimeHours");

            migrationBuilder.RenameColumn(
                name: "SpecialOTPay",
                table: "Payrolls",
                newName: "SpecialWorkingOTPay");

            migrationBuilder.RenameColumn(
                name: "SpecialNDPay",
                table: "Payrolls",
                newName: "SpecialWorkingNDPay");

            migrationBuilder.RenameColumn(
                name: "SpecialNDOTPay",
                table: "Payrolls",
                newName: "SpecialWorkingNDOTPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialPay",
                table: "Payrolls",
                newName: "SpecialWorkDayPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialOTPay",
                table: "Payrolls",
                newName: "SpecialWorkDayOTPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialNDPay",
                table: "Payrolls",
                newName: "SpecialWorkDayNDPay");

            migrationBuilder.RenameColumn(
                name: "RestSpecialNDOTPay",
                table: "Payrolls",
                newName: "SpecialWorkDayNDOTPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalPay",
                table: "Payrolls",
                newName: "SpecialNonWorkingOTPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalOTPay",
                table: "Payrolls",
                newName: "SpecialNonWorkingNDPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalNDPay",
                table: "Payrolls",
                newName: "SpecialNonWorkingNDOTPay");

            migrationBuilder.RenameColumn(
                name: "RestLegalNDOTPay",
                table: "Payrolls",
                newName: "SpecialHolidayPay");

            migrationBuilder.RenameColumn(
                name: "NDPay",
                table: "Payrolls",
                newName: "RestSpecialDayPay");

            migrationBuilder.RenameColumn(
                name: "NDOTPay",
                table: "Payrolls",
                newName: "RestSpecialDayOTPay");

            migrationBuilder.RenameColumn(
                name: "LegalPay",
                table: "Payrolls",
                newName: "RestSpecialDayNDPay");

            migrationBuilder.RenameColumn(
                name: "LegalOTPay",
                table: "Payrolls",
                newName: "RestSpecialDayNDOTPay");

            migrationBuilder.RenameColumn(
                name: "LegalNDPay",
                table: "Payrolls",
                newName: "RestLegalDayPay");

            migrationBuilder.RenameColumn(
                name: "LegalNDOTPay",
                table: "Payrolls",
                newName: "RestLegalDayOTPay");

            migrationBuilder.RenameColumn(
                name: "GrossPay",
                table: "Payrolls",
                newName: "RestLegalDayNDPay");

            migrationBuilder.RenameColumn(
                name: "AbsencesAmount",
                table: "Payrolls",
                newName: "RestLegalDayNDOTPay");

            migrationBuilder.AddColumn<decimal>(
                name: "Absences",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AbsentCount",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "DoubleLegalHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DoubleLegalNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DoubleLegalNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DoubleLegalOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "HolidayPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LateHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolNDOTPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolNDPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolNightDiffHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolNightDiffOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolOTPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolidayPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NightDifferentialHour",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NightDifferentialPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHour",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "RegularNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RegularNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RegularNetHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RegularOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDoubleLegalHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDoubleLegalNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDoubleLegalNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDoubleLegalOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolNightDiffHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolNightDiffOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialWorkDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialWorkDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialWorkDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialWorkDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
