using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class finalpay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConsumedByPayrollId",
                table: "SalaryAdjustments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ConsumedByPayrollId",
                table: "OtherIncomeApplicationDetails",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsumedByPayrollId",
                table: "SalaryAdjustments");

            migrationBuilder.DropColumn(
                name: "ConsumedByPayrollId",
                table: "OtherIncomeApplicationDetails");
        }
    }
}
