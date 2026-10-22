using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class employeeId_setting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmployeeIdIncludeYear",
                table: "Companies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeIdMode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "EmployeeIdPadding",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeIdPrefix",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "NextEmployeeNo",
                table: "Companies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ShowLetterhead",
                table: "Companies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeIdIncludeYear",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EmployeeIdMode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EmployeeIdPadding",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EmployeeIdPrefix",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "NextEmployeeNo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ShowLetterhead",
                table: "Companies");
        }
    }
}
