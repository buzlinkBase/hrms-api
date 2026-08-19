using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class missingtables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OvertimeHR",
                table: "Payrolls",
                newName: "Reimbursement");

            migrationBuilder.RenameColumn(
                name: "NightDifferentialHR",
                table: "Payrolls",
                newName: "OvertimeHour");

            migrationBuilder.AddColumn<double>(
                name: "LegalHolHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolNightDiffHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolNightDiffOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "LegalHolOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<decimal>(
                name: "NightDifferentialHour",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<double>(
                name: "RegularNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RegularNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RegularNetHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RegularOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestLegalDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayNDHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayNDOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "RestSpecialDayOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolNightDiffHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolNightDiffOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SpecialHolOTHours",
                table: "Payrolls",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "GovAnnualTaxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RangeFrom = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RangeTo = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    BaseTaxDue = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOnPercentage = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovAnnualTaxes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PassSlipApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReturnTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Destination = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
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
                    table.PrimaryKey("PK_PassSlipApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassSlipApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GovAnnualTaxes_TenantId_DeletedAt",
                table: "GovAnnualTaxes",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PassSlipApplications_EmployeeId",
                table: "PassSlipApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PassSlipApplications_TenantId_DeletedAt",
                table: "PassSlipApplications",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GovAnnualTaxes");

            migrationBuilder.DropTable(
                name: "PassSlipApplications");

            migrationBuilder.DropColumn(
                name: "LegalHolHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolNightDiffHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolNightDiffOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalHolOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "NightDifferentialHour",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNetHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalDayOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayNDHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayNDOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialDayOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolNightDiffHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolNightDiffOTHours",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialHolOTHours",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "Reimbursement",
                table: "Payrolls",
                newName: "OvertimeHR");

            migrationBuilder.RenameColumn(
                name: "OvertimeHour",
                table: "Payrolls",
                newName: "NightDifferentialHR");
        }
    }
}
