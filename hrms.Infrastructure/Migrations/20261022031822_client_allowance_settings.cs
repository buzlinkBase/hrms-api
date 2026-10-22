using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class client_allowance_settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodEnd",
                table: "UniformAllowanceLedgers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PeriodStart",
                table: "UniformAllowanceLedgers",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PolicyId",
                table: "UniformAllowanceLedgers",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateOnly>(
                name: "SeparatedOn",
                table: "UniformAllowanceFunds",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ServiceStartDate",
                table: "UniformAllowanceFunds",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UniformAllowanceCreditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PolicyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreditDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Outcome = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReasonText = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PresentDays = table.Column<int>(type: "int", nullable: true),
                    RequiredDays = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LedgerId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UniformAllowanceCreditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UniformAllowanceCreditLogs_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UniformAllowancePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Frequency = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreditDay = table.Column<int>(type: "int", nullable: false),
                    StartMonth = table.Column<int>(type: "int", nullable: false),
                    AmountMode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ExpectedDaysPerPeriod = table.Column<int>(type: "int", nullable: true),
                    EligibleStatuses = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WaitingMonths = table.Column<int>(type: "int", nullable: false),
                    MinPresentDays = table.Column<int>(type: "int", nullable: true),
                    MaxBalance = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Expiry = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiryMonth = table.Column<int>(type: "int", nullable: true),
                    ExpiryAfterMonths = table.Column<int>(type: "int", nullable: true),
                    OnSeparation = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RehireRule = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RehireBridgeDays = table.Column<int>(type: "int", nullable: true),
                    ChangeReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
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
                    table.PrimaryKey("PK_UniformAllowancePolicies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceCreditLogs_ClientId_CreditDate",
                table: "UniformAllowanceCreditLogs",
                columns: new[] { "ClientId", "CreditDate" });

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceCreditLogs_EmployeeId_CreditDate",
                table: "UniformAllowanceCreditLogs",
                columns: new[] { "EmployeeId", "CreditDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowanceCreditLogs_TenantId_DeletedAt",
                table: "UniformAllowanceCreditLogs",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowancePolicies_ClientId_EffectiveFrom",
                table: "UniformAllowancePolicies",
                columns: new[] { "ClientId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UniformAllowancePolicies_TenantId_DeletedAt",
                table: "UniformAllowancePolicies",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UniformAllowanceCreditLogs");

            migrationBuilder.DropTable(
                name: "UniformAllowancePolicies");

            migrationBuilder.DropColumn(
                name: "PeriodEnd",
                table: "UniformAllowanceLedgers");

            migrationBuilder.DropColumn(
                name: "PeriodStart",
                table: "UniformAllowanceLedgers");

            migrationBuilder.DropColumn(
                name: "PolicyId",
                table: "UniformAllowanceLedgers");

            migrationBuilder.DropColumn(
                name: "SeparatedOn",
                table: "UniformAllowanceFunds");

            migrationBuilder.DropColumn(
                name: "ServiceStartDate",
                table: "UniformAllowanceFunds");
        }
    }
}
