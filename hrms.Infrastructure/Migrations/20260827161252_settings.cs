using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyTimeRecords_EmployeeId",
                table: "DailyTimeRecords");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeRecords_EmployeeId_WorkDate",
                table: "DailyTimeRecords",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyTimeRecords_EmployeeId_WorkDate",
                table: "DailyTimeRecords");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeRecords_EmployeeId",
                table: "DailyTimeRecords",
                column: "EmployeeId");
        }
    }
}
