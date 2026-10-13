using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class nodtr_employee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoDtrCategory",
                table: "EmployeeSettings",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoDtrEffectiveFrom",
                table: "EmployeeSettings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "NoDtrEffectiveUntil",
                table: "EmployeeSettings",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoDtrCategory",
                table: "EmployeeSettings");

            migrationBuilder.DropColumn(
                name: "NoDtrEffectiveFrom",
                table: "EmployeeSettings");

            migrationBuilder.DropColumn(
                name: "NoDtrEffectiveUntil",
                table: "EmployeeSettings");
        }
    }
}
