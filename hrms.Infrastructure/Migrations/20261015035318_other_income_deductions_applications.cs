using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class other_income_deductions_applications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApplicableDays",
                table: "OtherIncomeApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceBasis",
                table: "OtherIncomeApplications",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "DayTypes",
                table: "OtherIncomeApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxLateMinutes",
                table: "OtherIncomeApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUndertimeMinutes",
                table: "OtherIncomeApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinHours",
                table: "OtherIncomeApplications",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinQualifyingDays",
                table: "OtherIncomeApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayMode",
                table: "OtherIncomeApplications",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "OtherIncomeApplicationDetails",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "OtherIncomeApplicationDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "OtherIncomeApplicationDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "OtherIncomeApplicationDetails",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "AllowAttendanceRules",
                table: "Deductions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ApplicableDays",
                table: "DeductionApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceBasis",
                table: "DeductionApplications",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "DayTypes",
                table: "DeductionApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxLateMinutes",
                table: "DeductionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxUndertimeMinutes",
                table: "DeductionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinHours",
                table: "DeductionApplications",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinQualifyingDays",
                table: "DeductionApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnMiss",
                table: "DeductionApplications",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PayMode",
                table: "DeductionApplications",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeferred",
                table: "DeductionApplicationDetails",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSkipped",
                table: "DeductionApplicationDetails",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "DeductionApplicationDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "DeductionApplicationDetails",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "DeductionApplicationDetails",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "SkippedByPayrollId",
                table: "DeductionApplicationDetails",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicableDays",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "AttendanceBasis",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "DayTypes",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "MaxLateMinutes",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "MaxUndertimeMinutes",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "MinHours",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "MinQualifyingDays",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "PayMode",
                table: "OtherIncomeApplications");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "AllowAttendanceRules",
                table: "Deductions");

            migrationBuilder.DropColumn(
                name: "ApplicableDays",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "AttendanceBasis",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "DayTypes",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "MaxLateMinutes",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "MaxUndertimeMinutes",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "MinHours",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "MinQualifyingDays",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "OnMiss",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "PayMode",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "IsDeferred",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropColumn(
                name: "IsSkipped",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropColumn(
                name: "SkippedByPayrollId",
                table: "DeductionApplicationDetails");
        }
    }
}
