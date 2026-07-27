using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class add_shiftId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "DailyTimeRecords",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ClientId",
                table: "Attendances",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_OperationAreaId",
                table: "Attendances",
                column: "OperationAreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Areas_OperationAreaId",
                table: "Attendances",
                column: "OperationAreaId",
                principalTable: "Areas",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Branches_BranchId",
                table: "Attendances",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Clients_ClientId",
                table: "Attendances",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Areas_OperationAreaId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Branches_BranchId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Clients_ClientId",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_ClientId",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_OperationAreaId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "DailyTimeRecords");
        }
    }
}
