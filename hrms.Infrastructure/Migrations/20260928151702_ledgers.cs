using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ledgers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Add",
                table: "UniformAllowanceLedgers");

            migrationBuilder.DropColumn(
                name: "Add",
                table: "RetirementLedgers");

            migrationBuilder.DropColumn(
                name: "Add",
                table: "LeaveLedgers");

            migrationBuilder.RenameColumn(
                name: "Less",
                table: "UniformAllowanceLedgers",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "Less",
                table: "RetirementLedgers",
                newName: "Amount");

            migrationBuilder.RenameColumn(
                name: "Less",
                table: "LeaveLedgers",
                newName: "Amount");

            migrationBuilder.CreateTable(
                name: "CashBondAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
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
                    table.PrimaryKey("PK_CashBondAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashBondAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CashBondAdjustments_EmployeeId",
                table: "CashBondAdjustments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CashBondAdjustments_TenantId_DeletedAt",
                table: "CashBondAdjustments",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashBondAdjustments");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "UniformAllowanceLedgers",
                newName: "Less");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "RetirementLedgers",
                newName: "Less");

            migrationBuilder.RenameColumn(
                name: "Amount",
                table: "LeaveLedgers",
                newName: "Less");

            migrationBuilder.AddColumn<decimal>(
                name: "Add",
                table: "UniformAllowanceLedgers",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Add",
                table: "RetirementLedgers",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Add",
                table: "LeaveLedgers",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
