using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ledger_add : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RetirementLedgers_Payrolls_PayrollId",
                table: "RetirementLedgers");

            migrationBuilder.AlterColumn<Guid>(
                name: "PayrollId",
                table: "RetirementLedgers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "EntryType",
                table: "RetirementLedgers",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddForeignKey(
                name: "FK_RetirementLedgers_Payrolls_PayrollId",
                table: "RetirementLedgers",
                column: "PayrollId",
                principalTable: "Payrolls",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RetirementLedgers_Payrolls_PayrollId",
                table: "RetirementLedgers");

            migrationBuilder.DropColumn(
                name: "EntryType",
                table: "RetirementLedgers");

            migrationBuilder.AlterColumn<Guid>(
                name: "PayrollId",
                table: "RetirementLedgers",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddForeignKey(
                name: "FK_RetirementLedgers_Payrolls_PayrollId",
                table: "RetirementLedgers",
                column: "PayrollId",
                principalTable: "Payrolls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
