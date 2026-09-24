using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class change_approver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReassignedApproverEmployeeId",
                table: "ApprovalInstances",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalInstances_ReassignedApproverEmployeeId",
                table: "ApprovalInstances",
                column: "ReassignedApproverEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalInstances_Employees_ReassignedApproverEmployeeId",
                table: "ApprovalInstances",
                column: "ReassignedApproverEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalInstances_Employees_ReassignedApproverEmployeeId",
                table: "ApprovalInstances");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalInstances_ReassignedApproverEmployeeId",
                table: "ApprovalInstances");

            migrationBuilder.DropColumn(
                name: "ReassignedApproverEmployeeId",
                table: "ApprovalInstances");
        }
    }
}
