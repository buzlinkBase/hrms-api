using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class rostering_tag_area : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperationAreaId",
                table: "WorkSchedulePlans",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedulePlans_OperationAreaId",
                table: "WorkSchedulePlans",
                column: "OperationAreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSchedulePlans_Areas_OperationAreaId",
                table: "WorkSchedulePlans",
                column: "OperationAreaId",
                principalTable: "Areas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkSchedulePlans_Areas_OperationAreaId",
                table: "WorkSchedulePlans");

            migrationBuilder.DropIndex(
                name: "IX_WorkSchedulePlans_OperationAreaId",
                table: "WorkSchedulePlans");

            migrationBuilder.DropColumn(
                name: "OperationAreaId",
                table: "WorkSchedulePlans");
        }
    }
}
