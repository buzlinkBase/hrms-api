using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class tenantconfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "UseEmployeeOverride",
                table: "Employees",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "PayrollInclusionDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DefaultRestDayPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultRegularHolidayIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultSpecialNonWorkingIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultNightDiffIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollInclusionDefaults", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeApplicationDetails_IncomeId",
                table: "OtherIncomeApplicationDetails",
                column: "IncomeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollInclusionDefaults_TenantId_DeletedAt",
                table: "PayrollInclusionDefaults",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_OtherIncomeApplicationDetails_Allowances_IncomeId",
                table: "OtherIncomeApplicationDetails",
                column: "IncomeId",
                principalTable: "Allowances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OtherIncomeApplicationDetails_Allowances_IncomeId",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropTable(
                name: "PayrollInclusionDefaults");

            migrationBuilder.DropIndex(
                name: "IX_OtherIncomeApplicationDetails_IncomeId",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "UseEmployeeOverride",
                table: "Employees");
        }
    }
}
