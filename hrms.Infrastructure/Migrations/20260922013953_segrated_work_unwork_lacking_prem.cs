using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class segrated_work_unwork_lacking_prem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DoubleLegalUnworkedPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestDoubleLegalUnworkedPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalUnworkedPay",
                table: "Payrolls",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalUnWorked",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RestLegalWorked",
                table: "PayrollDtrDetails",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoubleLegalUnworkedPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestDoubleLegalUnworkedPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalUnworkedPay",
                table: "Payrolls");

            migrationBuilder.DropColumn(
                name: "RestLegalUnWorked",
                table: "PayrollDtrDetails");

            migrationBuilder.DropColumn(
                name: "RestLegalWorked",
                table: "PayrollDtrDetails");
        }
    }
}
