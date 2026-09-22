using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class segrated_premium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDOTPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDPremiumPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialOTBasePay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LegalOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RegularOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDayOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestSpecialOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDOTPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialNDPremiumPay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SpecialOTBasePay",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoubleLegalNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "LegalOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RegularOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDayOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestSpecialOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialNDBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialNDOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialNDOTPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialNDPremiumPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "SpecialOTBasePay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "DoubleLegalNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "DoubleLegalOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "LegalNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "LegalNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "LegalNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "LegalNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "LegalOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RegularNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RegularNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RegularNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RegularNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RegularOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDayNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDayNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDayNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDayNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDayOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestLegalNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestLegalNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestLegalNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestLegalNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestLegalOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestSpecialNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestSpecialOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "SpecialNDBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "SpecialNDOTBasePay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "SpecialNDOTPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "SpecialNDPremiumPay",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "SpecialOTBasePay",
                table: "PayrollDtrDetails");
        }
    }
}
