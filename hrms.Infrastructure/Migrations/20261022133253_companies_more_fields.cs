using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class companies_more_fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtcCode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "BusinessPermitExpiry",
                table: "Companies",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessPermitNo",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "EmployerType",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsWithholdingAgent",
                table: "Companies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PagIbigBranchCode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PayslipFooter",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhilHealthBranchCode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationDate",
                table: "Companies",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecDtiNumber",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SssBranchCode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TinBranchCode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtcCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BusinessPermitExpiry",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "BusinessPermitNo",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "EmployerType",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsWithholdingAgent",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PagIbigBranchCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PayslipFooter",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PhilHealthBranchCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RegistrationDate",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SecDtiNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SssBranchCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TinBranchCode",
                table: "Companies");
        }
    }
}
