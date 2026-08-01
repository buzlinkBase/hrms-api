using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class updatemodel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "WorkSchedulePlans",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UTApplications",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TimeShifts",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ThirteenthMonthLedgers",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TaxRates",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "TaxContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SSSRates",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "SSSContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Skills",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Sections",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RestDays",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "RestDayDates",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ProratedAllowances",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PremiumRates",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Positions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PHICRates",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PHICContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Payrolls",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "PayrollGroups",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OtherIncomeApplications",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OtherIncomeApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OTApplications",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ManualAttendance",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Leaves",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LeaveLedgers",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "leaveApplications",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "LeaveApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "IncomePayments",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Holidays",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "HDMFRates",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "HDMFContributions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GovTaxes",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GovSSSes",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GovPHICs",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GovHDMFs",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GeneralSettings",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Employments",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EmployeeSettings",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Employees",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EmployeeRecords",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Educations",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Dependents",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Departments",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeductionTypes",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Deductions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeductionPayments",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeductionApplications",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DeductionApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "DailyTimeRecords",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CutoffDay",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Companies",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Clients",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ClientHolidays",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ChangeRestDays",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "ChangeHolidays",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Branches",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Attendances",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AssignAssets",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Areas",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AllowanceTypes",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Allowances",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("0123f5e6-d7c8-4234-bcda-6789012345fa"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("1234a5b6-c7d8-4345-cdab-7890123456ab"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("2345b6c7-d8e9-4456-dabc-8901234567bc"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("3456c7d8-e9f0-4567-abcd-9012345678cd"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("4567d8e9-f012-4678-bcda-0123456789de"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("a2618e39-1a02-4055-989a-7b3bcc61f7b3"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("b1a2c3d4-e5f6-4789-abcd-1234567890ab"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("c2b3a4d5-f6e7-4890-bcda-2345678901bc"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("d3c4b5a6-e7f8-4901-cdab-3456789012cd"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("e4d5c6b7-f8e9-4012-dabc-4567890123de"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("f5e6d7c8-9012-4123-abcd-5678901234ef"),
                column: "TenantId",
                value: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WorkSchedulePlans");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UTApplications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TimeShifts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ThirteenthMonthLedgers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TaxRates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TaxContributions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SSSRates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SSSContributions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RestDays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RestDayDates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProratedAllowances");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PremiumRates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PHICRates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PHICContributions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PayrollGroups");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OTApplications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ManualAttendance");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Leaves");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LeaveLedgers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LeaveApplicationDetails");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IncomePayments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "HDMFRates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "HDMFContributions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GovTaxes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GovSSSes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GovPHICs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GovHDMFs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Employments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmployeeSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmployeeRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Educations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Dependents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeductionTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Deductions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeductionPayments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DailyTimeRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CutoffDay");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ClientHolidays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ChangeRestDays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ChangeHolidays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AssignAssets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AllowanceTypes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Allowances");
        }
    }
}
