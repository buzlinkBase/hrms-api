using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class beginning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    BasicPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OvertimePay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    HolidayPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Allowances = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OtherIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Bonuses = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    GrossIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NonTaxableIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SSSContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PhilHealthContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PagIbigContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    WithholdingTax = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollOpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollOpeningBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PriorEmployerTaxRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    HasPriorEmployer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PriorEmployerName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrossIncomeYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NonTaxableYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    StatutoryDeductionsYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TaxWithheldYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorEmployerTaxRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriorEmployerTaxRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "YearLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearLocks", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOpeningBalances_EmployeeId_Year",
                table: "PayrollOpeningBalances",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOpeningBalances_TenantId_DeletedAt",
                table: "PayrollOpeningBalances",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriorEmployerTaxRecords_EmployeeId_Year",
                table: "PriorEmployerTaxRecords",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriorEmployerTaxRecords_TenantId_DeletedAt",
                table: "PriorEmployerTaxRecords",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearLocks_TenantId_DeletedAt",
                table: "YearLocks",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearLocks_TenantId_Year",
                table: "YearLocks",
                columns: new[] { "TenantId", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollOpeningBalances");

            migrationBuilder.DropTable(
                name: "PriorEmployerTaxRecords");

            migrationBuilder.DropTable(
                name: "YearLocks");
        }
    }
}
