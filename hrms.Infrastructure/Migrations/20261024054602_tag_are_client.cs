using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class tag_are_client : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientId",
                table: "Areas",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_ClientId",
                table: "Areas",
                column: "ClientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Areas_Clients_ClientId",
                table: "Areas",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Clients_ClientId",
                table: "Areas");

            migrationBuilder.DropIndex(
                name: "IX_Areas_ClientId",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "Areas");
        }
    }
}
