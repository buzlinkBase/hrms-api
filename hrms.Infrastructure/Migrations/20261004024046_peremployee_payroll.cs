using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class peremployee_payroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "DailyTimeRecords",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeRecords_PayrollBatchId",
                table: "DailyTimeRecords",
                column: "PayrollBatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyTimeRecords_PayrollBatchId",
                table: "DailyTimeRecords");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "DailyTimeRecords");
        }
    }
}
