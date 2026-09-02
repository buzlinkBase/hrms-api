using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class projectsite_atg_branch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolNightDiffHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolNightDiffOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalHolOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NonCompanyPaidLeaves",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OBHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OneTimePayoutBreakdown",
                table: "Payrolls",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "OvertimeHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaidLeaveBreakdown",
                table: "Payrolls",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "PaidLeaveHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNetHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalDayHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalDayNDHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalDayNDOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalDayOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialDayHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialDayNDHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialDayNDOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialDayOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialHolHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialHolNightDiffHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialHolNightDiffOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialHolOTHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnpaidLeaveHours",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Branches",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Areas",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_BranchId",
                table: "Areas",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Branches_BranchId",
                table: "Areas",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Branches_BranchId",
                table: "Areas");

            migrationBuilder.DropIndex(
                name: "IX_Areas_BranchId",
                table: "Areas");

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
                name: "LegalHolHours",
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
                name: "NonCompanyPaidLeaves",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "OBHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "OneTimePayoutBreakdown",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "OvertimeHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "PaidLeaveBreakdown",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "PaidLeaveHours",
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
                name: "UnpaidLeaveHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Areas");

            migrationBuilder.UpdateData(
                table: "Branches",
                keyColumn: "Email",
                keyValue: null,
                column: "Email",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Branches",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
