using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class tag_client_rostering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "WorkSchedulePlans",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedulePlans_ClientId",
                table: "WorkSchedulePlans",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSchedulePlans_Clients_ClientId",
                table: "WorkSchedulePlans",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkSchedulePlans_Clients_ClientId",
                table: "WorkSchedulePlans");

            migrationBuilder.DropIndex(
                name: "IX_WorkSchedulePlans_ClientId",
                table: "WorkSchedulePlans");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "WorkSchedulePlans");
        }
    }
}
