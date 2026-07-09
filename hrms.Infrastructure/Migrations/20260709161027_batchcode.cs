using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class batchcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BatchCode",
                table: "Attendances",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Att_BRId_DepId_Area_ClId_LS",
                table: "Attendances",
                columns: new[] { "BranchId", "DepartmentId", "OperationAreaId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_LogSource_BatchCode",
                table: "Attendances",
                columns: new[] { "LogSource", "BatchCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_ls_wt",
                table: "Attendances",
                columns: new[] { "LogSource", "WorkDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Att_BRId_DepId_Area_ClId_LS",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_LogSource_BatchCode",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_ls_wt",
                table: "Attendances");

            migrationBuilder.AlterColumn<Guid>(
                name: "BatchCode",
                table: "Attendances",
                type: "char(36)",
                nullable: false,
                collation: "ascii_general_ci",
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
