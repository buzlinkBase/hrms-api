using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class dtr_payroll_batch_marker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PayrollBatchId",
                table: "DailyTimeRecords",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeRecords_PayrollBatchId",
                table: "DailyTimeRecords",
                column: "PayrollBatchId");

            // Backfill: days of every DTR batch an existing payroll run already used count as paid,
            // so the new cutoff-based run can't pay them a second time. PayrollBatches.DtrBatchCodes
            // is a comma-separated list; FIND_IN_SET matches one code at a time. A code is matched
            // only inside its own tenant. A code shared by two runs takes the earliest-created one.
            migrationBuilder.Sql(@"
UPDATE `DailyTimeRecords` d
JOIN (
    SELECT dr.`Id` AS DayId, MIN(pb.`Id`) AS BatchId
    FROM `DailyTimeRecords` dr
    JOIN `PayrollBatches` pb
      ON pb.`TenantId` = dr.`TenantId`
     AND pb.`DtrBatchCodes` IS NOT NULL AND pb.`DtrBatchCodes` <> ''
     AND FIND_IN_SET(dr.`BatchCode`, pb.`DtrBatchCodes`) > 0
    WHERE dr.`BatchCode` IS NOT NULL
    GROUP BY dr.`Id`
) m ON m.DayId = d.`Id`
SET d.`PayrollBatchId` = m.BatchId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DailyTimeRecords_PayrollBatchId",
                table: "DailyTimeRecords");

            migrationBuilder.DropColumn(
                name: "PayrollBatchId",
                table: "DailyTimeRecords");
        }
    }
}
