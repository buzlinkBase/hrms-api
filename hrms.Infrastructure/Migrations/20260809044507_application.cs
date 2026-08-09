using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class application : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlexiEndTime",
                table: "OTApplications");

            migrationBuilder.DropColumn(
                name: "OTBeforeOverride",
                table: "OTApplications");

            migrationBuilder.RenameColumn(
                name: "PaidByNetDutyTime",
                table: "OTApplications",
                newName: "IsManualEntry");

            migrationBuilder.RenameColumn(
                name: "OTMinutes",
                table: "OTApplications",
                newName: "ManualOTMinutes");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartTime",
                table: "OTApplications",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndTime",
                table: "OTApplications",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ManualOTMinutes",
                table: "OTApplications",
                newName: "OTMinutes");

            migrationBuilder.RenameColumn(
                name: "IsManualEntry",
                table: "OTApplications",
                newName: "PaidByNetDutyTime");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartTime",
                table: "OTApplications",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndTime",
                table: "OTApplications",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FlexiEndTime",
                table: "OTApplications",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "OTBeforeOverride",
                table: "OTApplications",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
