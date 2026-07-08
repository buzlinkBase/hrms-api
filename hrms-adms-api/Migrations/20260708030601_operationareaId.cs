using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.adms.Migrations
{
    /// <inheritdoc />
    public partial class operationareaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OperationAreaId",
                table: "BiometricDevices",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "OperationAreaId",
                table: "Attendances",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OperationAreaId",
                table: "BiometricDevices");

            migrationBuilder.DropColumn(
                name: "OperationAreaId",
                table: "Attendances");
        }
    }
}
