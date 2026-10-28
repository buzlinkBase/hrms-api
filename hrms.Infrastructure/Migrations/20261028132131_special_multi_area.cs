using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class special_multi_area : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Holidays_Areas_AreaId",
                table: "Holidays");

            migrationBuilder.DropIndex(
                name: "IX_Holidays_AreaId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "AreaId",
                table: "Holidays");

            migrationBuilder.CreateTable(
                name: "HolidayAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    HolidayId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AreaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HolidayAreas_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HolidayAreas_Holidays_HolidayId",
                        column: x => x.HolidayId,
                        principalTable: "Holidays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayAreas_AreaId",
                table: "HolidayAreas",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayAreas_HolidayId_AreaId",
                table: "HolidayAreas",
                columns: new[] { "HolidayId", "AreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayAreas_TenantId_DeletedAt",
                table: "HolidayAreas",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HolidayAreas");

            migrationBuilder.AddColumn<Guid>(
                name: "AreaId",
                table: "Holidays",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_AreaId",
                table: "Holidays",
                column: "AreaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Holidays_Areas_AreaId",
                table: "Holidays",
                column: "AreaId",
                principalTable: "Areas",
                principalColumn: "Id");
        }
    }
}
