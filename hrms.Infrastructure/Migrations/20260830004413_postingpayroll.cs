using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class postingpayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DtrBatchCodes",
                table: "Payrolls",
                newName: "Remarks");

            migrationBuilder.AlterColumn<string>(
                name: "BatchCode",
                table: "Payrolls",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "Payrolls",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "PayrollBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayPeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PayPeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DtrBatchCodes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPosted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PostedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBatches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_TenantId_DeletedAt",
                table: "PayrollBatches",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "Payrolls");

            migrationBuilder.RenameColumn(
                name: "Remarks",
                table: "Payrolls",
                newName: "DtrBatchCodes");

            migrationBuilder.AlterColumn<Guid>(
                name: "BatchCode",
                table: "Payrolls",
                type: "char(36)",
                nullable: false,
                collation: "ascii_general_ci",
                oldClrType: typeof(string),
                oldType: "longtext")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
