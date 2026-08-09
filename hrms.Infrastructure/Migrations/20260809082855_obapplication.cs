using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class obapplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Days",
                table: "TravelOrderApplications");

            migrationBuilder.DropColumn(
                name: "TravelDayType",
                table: "TravelOrderApplications");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "StartDate",
                table: "TravelOrderApplications",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "EndDate",
                table: "TravelOrderApplications",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "TravelOrderApplications",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsManualEntry",
                table: "TravelOrderApplications",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "TravelOrderApplications",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "TotalMinutes",
                table: "TravelOrderApplications",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "TravelOrderApplications");

            migrationBuilder.DropColumn(
                name: "IsManualEntry",
                table: "TravelOrderApplications");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "TravelOrderApplications");

            migrationBuilder.DropColumn(
                name: "TotalMinutes",
                table: "TravelOrderApplications");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "TravelOrderApplications",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "TravelOrderApplications",
                type: "datetime(6)",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AddColumn<int>(
                name: "Days",
                table: "TravelOrderApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TravelDayType",
                table: "TravelOrderApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
