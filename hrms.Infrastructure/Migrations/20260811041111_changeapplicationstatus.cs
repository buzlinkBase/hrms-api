using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class changeapplicationstatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OTStatus",
                table: "UTApplications",
                newName: "ApprovalStatus");

            migrationBuilder.RenameColumn(
                name: "OTStatus",
                table: "OTApplications",
                newName: "ApprovalStatus");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "TravelOrderApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "TravelOrderApplications");

            migrationBuilder.RenameColumn(
                name: "ApprovalStatus",
                table: "UTApplications",
                newName: "OTStatus");

            migrationBuilder.RenameColumn(
                name: "ApprovalStatus",
                table: "OTApplications",
                newName: "OTStatus");
        }
    }
}
