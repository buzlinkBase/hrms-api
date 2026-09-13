using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class policy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollDeductionDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeductionApplicationDetailId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeductionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDeductionDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollDeductionDetails_Payrolls_PayrollId",
                        column: x => x.PayrollId,
                        principalTable: "Payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollDtrDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DtrId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DTRRef = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PayrollGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DailyRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SalaryType = table.Column<int>(type: "int", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    LateAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    UTAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AbsentAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PaidLeave = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    UnpaidLeave = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularDayPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalWorked = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalUnWorked = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalWorked = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalUnworked = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalWorked = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalUnworked = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalExcludingBasic = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Holiday = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NDPremiumPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OTPremiumPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalOT = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalND = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalNDOT = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollDtrDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollDtrDetails_Payrolls_PayrollId",
                        column: x => x.PayrollId,
                        principalTable: "Payrolls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductionDetails_PayrollId",
                table: "PayrollDeductionDetails",
                column: "PayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDeductionDetails_TenantId_DeletedAt",
                table: "PayrollDeductionDetails",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDtrDetails_PayrollId",
                table: "PayrollDtrDetails",
                column: "PayrollId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollDtrDetails_TenantId_DeletedAt",
                table: "PayrollDtrDetails",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollDeductionDetails");

            migrationBuilder.DropTable(
                name: "PayrollDtrDetails");
        }
    }
}
