using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class newfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollInclusionDefaults");

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
                keyValue: new Guid("4567d8e9-f0a1-4678-bcde-0123456789de"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("5678e9f0-a1b2-4789-cdef-1234567890ef"));

            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("6789f0a1-b2c3-489a-defa-2345678901fa"));

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

            migrationBuilder.DropColumn(
                name: "IsNightDiffIncluded",
                table: "Employees");

            migrationBuilder.AddColumn<bool>(
                name: "EmployerAdvancesPayment",
                table: "leaveApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReimbursementFiledDate",
                table: "leaveApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ReimbursementReceivedDate",
                table: "leaveApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReimbursementReferenceNo",
                table: "leaveApplications",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ReimbursementStatus",
                table: "leaveApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "IdentityTypeId",
                table: "GeneralSettings",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityType",
                table: "GeneralSettings",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GeneralSettings_IdentityType_IdentityTypeId",
                table: "GeneralSettings",
                columns: new[] { "IdentityType", "IdentityTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GeneralSettings_IdentityType_IdentityTypeId",
                table: "GeneralSettings");

            migrationBuilder.DropColumn(
                name: "EmployerAdvancesPayment",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "ReimbursementFiledDate",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "ReimbursementReceivedDate",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "ReimbursementReferenceNo",
                table: "leaveApplications");

            migrationBuilder.DropColumn(
                name: "ReimbursementStatus",
                table: "leaveApplications");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityTypeId",
                table: "GeneralSettings",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityType",
                table: "GeneralSettings",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsNightDiffIncluded",
                table: "Employees",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PayrollInclusionDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DefaultNightDiffIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultRegularHolidayIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultRestDayPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultSpecialNonWorkingIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollInclusionDefaults", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "GeneralSettings",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Description", "IdentityType", "IdentityTypeId", "Metadata", "Status", "TenantId", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { new Guid("0123f5e6-d7c8-4234-bcda-6789012345fa"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "NightDiffThreshold", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "0" },
                    { new Guid("1234a5b6-c7d8-4345-cdab-7890123456ab"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "AttFillLimit", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "NOLIMIT" },
                    { new Guid("2345b6c7-d8e9-4456-dabc-8901234567bc"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "HolidayTimeBasis", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "BasedOnTimeInDayType" },
                    { new Guid("3456c7d8-e9f0-4567-abcd-9012345678cd"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "IsHolPlusReg", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "False" },
                    { new Guid("4567d8e9-f0a1-4678-bcde-0123456789de"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "CrossMonthStatutoryCreditPolicy", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "CutoffStartMonth" },
                    { new Guid("5678e9f0-a1b2-4789-cdef-1234567890ef"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "WTaxCrossMonthCreditPolicy", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "CutoffEndMonth" },
                    { new Guid("6789f0a1-b2c3-489a-defa-2345678901fa"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "TreatNdotAsNdOnly", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "False" },
                    { new Guid("a2618e39-1a02-4055-989a-7b3bcc61f7b3"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "OTEligibility", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "IndependentOfAttendanceIssues" },
                    { new Guid("b1a2c3d4-e5f6-4789-abcd-1234567890ab"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "OTInclusion", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "UsePostShiftWork" },
                    { new Guid("c2b3a4d5-f6e7-4890-bcda-2345678901bc"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "IsHalfDayLateOn", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "False" },
                    { new Guid("d3c4b5a6-e7f8-4901-cdab-3456789012cd"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "HalfDayLateThresholdMinutes", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "0" },
                    { new Guid("e4d5c6b7-f8e9-4012-dabc-4567890123de"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "IsWholeDayLateOn", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "False" },
                    { new Guid("f5e6d7c8-9012-4123-abcd-5678901234ef"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "WholeDayLateThresholdMinutes", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "0" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollInclusionDefaults_TenantId_DeletedAt",
                table: "PayrollInclusionDefaults",
                columns: new[] { "TenantId", "DeletedAt" });
        }
    }
}
