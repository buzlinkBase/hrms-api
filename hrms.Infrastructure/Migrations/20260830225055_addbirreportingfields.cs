using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addbirreportingfields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NonTaxableIncome",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxableIncome",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "RDOCode",
                table: "Employees",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizedSignatoryName",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "AuthorizedSignatoryTitle",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PagIbigNumber",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PhilHealthNumber",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RDOCode",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SSSNumber",
                table: "Companies",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NonTaxableIncome",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "TaxableIncome",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RDOCode",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AuthorizedSignatoryName",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AuthorizedSignatoryTitle",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PagIbigNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PhilHealthNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "RDOCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SSSNumber",
                table: "Companies");
        }
    }
}
