using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class benefit_ledger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RetirementAmount",
                table: "Clients",
                newName: "RetirementDaysPerYear");

            migrationBuilder.AddColumn<decimal>(
                name: "RetirementAccrual",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetirementPayout",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "RetirementFunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Balance = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetirementFunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetirementFunds_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UniformAllowanceFunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Balance = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniformAllowanceFunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniformAllowanceFunds_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RetirementLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RetirementFundId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Add = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Less = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Particulars = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetirementLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetirementLedgers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetirementLedgers_Payrolls_PayrollId",
                        column: x => x.PayrollId,
                        principalTable: "Payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetirementLedgers_RetirementFunds_RetirementFundId",
                        column: x => x.RetirementFundId,
                        principalTable: "RetirementFunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UniformAllowanceLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UniformAllowanceFundId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EntryType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Add = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Less = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Particulars = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniformAllowanceLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniformAllowanceLedgers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UniformAllowanceLedgers_UniformAllowanceFunds_UniformAllowan~",
                        column: x => x.UniformAllowanceFundId,
                        principalTable: "UniformAllowanceFunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementFunds_EmployeeId",
                table: "RetirementFunds",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetirementFunds_TenantId_DeletedAt",
                table: "RetirementFunds",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RetirementLedgers_EmployeeId",
                table: "RetirementLedgers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementLedgers_PayrollId",
                table: "RetirementLedgers",
                column: "PayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementLedgers_RetirementFundId",
                table: "RetirementLedgers",
                column: "RetirementFundId");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementLedgers_TenantId_DeletedAt",
                table: "RetirementLedgers",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceFunds_EmployeeId",
                table: "UniformAllowanceFunds",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceFunds_TenantId_DeletedAt",
                table: "UniformAllowanceFunds",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceLedgers_EmployeeId",
                table: "UniformAllowanceLedgers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceLedgers_TenantId_DeletedAt",
                table: "UniformAllowanceLedgers",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceLedgers_UniformAllowanceFundId",
                table: "UniformAllowanceLedgers",
                column: "UniformAllowanceFundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetirementLedgers");

            migrationBuilder.DropTable(
                name: "UniformAllowanceLedgers");

            migrationBuilder.DropTable(
                name: "RetirementFunds");

            migrationBuilder.DropTable(
                name: "UniformAllowanceFunds");

            migrationBuilder.DropColumn(
                name: "RetirementAccrual",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RetirementPayout",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "RetirementDaysPerYear",
                table: "Clients",
                newName: "RetirementAmount");
        }
    }
}
