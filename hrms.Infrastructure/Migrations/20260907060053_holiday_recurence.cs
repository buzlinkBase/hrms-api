using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class holiday_recurence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DayOfWeek",
                table: "Holidays",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeekOfMonth",
                table: "Holidays",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "WeekOfMonth",
                table: "Holidays");
        }
    }
}
