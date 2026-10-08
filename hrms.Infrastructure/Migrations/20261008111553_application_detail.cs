using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class application_detail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeductionApplicationDetails_DeductionApplications_DeductionA~",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OtherIncomeApplicationDetails_OtherIncomeApplications_OtherI~",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.DropColumn(
                name: "ApplicationId",
                table: "DeductionApplicationDetails");

            migrationBuilder.AlterColumn<Guid>(
                name: "OtherIncomeApplicationId",
                table: "OtherIncomeApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeductionApplicationId",
                table: "DeductionApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddForeignKey(
                name: "FK_DeductionApplicationDetails_DeductionApplications_DeductionA~",
                table: "DeductionApplicationDetails",
                column: "DeductionApplicationId",
                principalTable: "DeductionApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OtherIncomeApplicationDetails_OtherIncomeApplications_OtherI~",
                table: "OtherIncomeApplicationDetails",
                column: "OtherIncomeApplicationId",
                principalTable: "OtherIncomeApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeductionApplicationDetails_DeductionApplications_DeductionA~",
                table: "DeductionApplicationDetails");

            migrationBuilder.DropForeignKey(
                name: "FK_OtherIncomeApplicationDetails_OtherIncomeApplications_OtherI~",
                table: "OtherIncomeApplicationDetails");

            migrationBuilder.AlterColumn<Guid>(
                name: "OtherIncomeApplicationId",
                table: "OtherIncomeApplicationDetails",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "OtherIncomeApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AlterColumn<Guid>(
                name: "DeductionApplicationId",
                table: "DeductionApplicationDetails",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationId",
                table: "DeductionApplicationDetails",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddForeignKey(
                name: "FK_DeductionApplicationDetails_DeductionApplications_DeductionA~",
                table: "DeductionApplicationDetails",
                column: "DeductionApplicationId",
                principalTable: "DeductionApplications",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OtherIncomeApplicationDetails_OtherIncomeApplications_OtherI~",
                table: "OtherIncomeApplicationDetails",
                column: "OtherIncomeApplicationId",
                principalTable: "OtherIncomeApplications",
                principalColumn: "Id");
        }
    }
}
