using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class managerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_HeadId",
                table: "Departments");

            migrationBuilder.DropIndex(
                name: "IX_Departments_HeadId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "HeadId",
                table: "Departments");

            migrationBuilder.AddColumn<string>(
                name: "AccrualDedupeKey",
                table: "UniformAllowanceLedgers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AccrualDedupeKey",
                table: "LeaveLedgers",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "ManagerId",
                table: "Employees",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceLedgers_AccrualDedupeKey",
                table: "UniformAllowanceLedgers",
                column: "AccrualDedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_AccrualDedupeKey",
                table: "LeaveLedgers",
                column: "AccrualDedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_UniformAllowanceLedgers_AccrualDedupeKey",
                table: "UniformAllowanceLedgers");

            migrationBuilder.DropIndex(
                name: "IX_LeaveLedgers_AccrualDedupeKey",
                table: "LeaveLedgers");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AccrualDedupeKey",
                table: "UniformAllowanceLedgers");

            migrationBuilder.DropColumn(
                name: "AccrualDedupeKey",
                table: "LeaveLedgers");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Employees");

            migrationBuilder.AddColumn<Guid>(
                name: "HeadId",
                table: "Departments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_HeadId",
                table: "Departments",
                column: "HeadId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_HeadId",
                table: "Departments",
                column: "HeadId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
