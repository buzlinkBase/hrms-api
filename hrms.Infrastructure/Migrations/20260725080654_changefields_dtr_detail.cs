using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class changefields_dtr_detail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UTHours",
                table: "DailyTimeRecords",
                newName: "UTMinutes");

            migrationBuilder.RenameColumn(
                name: "OverBreakHours",
                table: "DailyTimeRecords",
                newName: "OverMinutes");

            migrationBuilder.RenameColumn(
                name: "OB",
                table: "DailyTimeRecords",
                newName: "OBHours");

            migrationBuilder.RenameColumn(
                name: "LateHours",
                table: "DailyTimeRecords",
                newName: "LateMinutes");

            migrationBuilder.RenameColumn(
                name: "LateForOTHours",
                table: "DailyTimeRecords",
                newName: "LateForOTMinutes");

            migrationBuilder.RenameColumn(
                name: "Absent",
                table: "DailyTimeRecords",
                newName: "WorkTypeEnum");

            migrationBuilder.AddColumn<int>(
                name: "AbsentCount",
                table: "DailyTimeRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("3456c7d8-e9f0-4567-abcd-9012345678cd"),
                column: "Value",
                value: "False");

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("4567d8e9-f012-4678-bcda-0123456789de"),
                column: "Value",
                value: "NoCredit");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentCount",
                table: "DailyTimeRecords");

            migrationBuilder.RenameColumn(
                name: "WorkTypeEnum",
                table: "DailyTimeRecords",
                newName: "Absent");

            migrationBuilder.RenameColumn(
                name: "UTMinutes",
                table: "DailyTimeRecords",
                newName: "UTHours");

            migrationBuilder.RenameColumn(
                name: "OverMinutes",
                table: "DailyTimeRecords",
                newName: "OverBreakHours");

            migrationBuilder.RenameColumn(
                name: "OBHours",
                table: "DailyTimeRecords",
                newName: "OB");

            migrationBuilder.RenameColumn(
                name: "LateMinutes",
                table: "DailyTimeRecords",
                newName: "LateHours");

            migrationBuilder.RenameColumn(
                name: "LateForOTMinutes",
                table: "DailyTimeRecords",
                newName: "LateForOTHours");

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("3456c7d8-e9f0-4567-abcd-9012345678cd"),
                column: "Value",
                value: "True");

            migrationBuilder.UpdateData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("4567d8e9-f012-4678-bcda-0123456789de"),
                column: "Value",
                value: "AutoCredit");
        }
    }
}
