using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class general_settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "GeneralSettings",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Description", "IdentityType", "IdentityTypeId", "Metadata", "Status", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { new Guid("0123f5e6-d7c8-4234-bcda-6789012345fa"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "NightDiffThreshold", "Company", null, null, "Active", null, "0" },
                    { new Guid("1234a5b6-c7d8-4345-cdab-7890123456ab"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "AttFillLimit", "Company", null, null, "Active", null, "NOLIMIT" },
                    { new Guid("2345b6c7-d8e9-4456-dabc-8901234567bc"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "HolidayTimeBasis", "Company", null, null, "Active", null, "BasedOnTimeInDayType" },
                    { new Guid("3456c7d8-e9f0-4567-abcd-9012345678cd"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "IsHolPlusReg", "Company", null, null, "Active", null, "True" },
                    { new Guid("4567d8e9-f012-4678-bcda-0123456789de"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "HolidayColumnPresentation", "Company", null, null, "Active", null, "AutoCredit" },
                    { new Guid("a2618e39-1a02-4055-989a-7b3bcc61f7b3"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "OTEligibility", "Company", null, null, "Active", null, "IndependentOfAttendanceIssues" },
                    { new Guid("b1a2c3d4-e5f6-4789-abcd-1234567890ab"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "OTInclusion", "Company", null, null, "Active", null, "UsePostShiftWork" },
                    { new Guid("c2b3a4d5-f6e7-4890-bcda-2345678901bc"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "IsHalfDayLateOn", "Company", null, null, "Active", null, "False" },
                    { new Guid("d3c4b5a6-e7f8-4901-cdab-3456789012cd"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "HalfDayLateThresholdMinutes", "Company", null, null, "Active", null, "0" },
                    { new Guid("e4d5c6b7-f8e9-4012-dabc-4567890123de"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "IsWholeDayLateOn", "Company", null, null, "Active", null, "False" },
                    { new Guid("f5e6d7c8-9012-4123-abcd-5678901234ef"), new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), null, "WholeDayLateThresholdMinutes", "Company", null, null, "Active", null, "0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("0123f5e6-d7c8-4234-bcda-6789012345fa"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("1234a5b6-c7d8-4345-cdab-7890123456ab"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("2345b6c7-d8e9-4456-dabc-8901234567bc"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("3456c7d8-e9f0-4567-abcd-9012345678cd"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("4567d8e9-f012-4678-bcda-0123456789de"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("a2618e39-1a02-4055-989a-7b3bcc61f7b3"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("b1a2c3d4-e5f6-4789-abcd-1234567890ab"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("c2b3a4d5-f6e7-4890-bcda-2345678901bc"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("d3c4b5a6-e7f8-4901-cdab-3456789012cd"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("e4d5c6b7-f8e9-4012-dabc-4567890123de"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("f5e6d7c8-9012-4123-abcd-5678901234ef"));
        }
    }
}
