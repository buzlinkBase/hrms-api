using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class employee_portal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue: 1 (ApprovalStatus.Approved), not 0 (ForApproval) — these columns
            // backfill EXISTING rows, all of which predate the self-service ForApproval workflow
            // and were created via the admin flow that always took immediate effect. Backfilling
            // to ForApproval would silently drop every pre-existing row out of
            // ChangeRestDayService.GetChangeRestDays / DeductionAplDtlService.LoadAsync.
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "DeductionApplications",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "ChangeRestDays",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "DeductionApplications");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "ChangeRestDays");
        }
    }
}
