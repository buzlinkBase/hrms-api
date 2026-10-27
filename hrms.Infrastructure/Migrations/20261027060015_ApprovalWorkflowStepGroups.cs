using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalWorkflowStepGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HeadEmployeeId",
                table: "Clients",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "HeadEmployeeId",
                table: "Branches",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "HeadEmployeeId",
                table: "Areas",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "ApproverLevel",
                table: "ApprovalWorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConditionMetric",
                table: "ApprovalWorkflowSteps",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ConditionOperator",
                table: "ApprovalWorkflowSteps",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "ConditionValue",
                table: "ApprovalWorkflowSteps",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EscalateAfterHours",
                table: "ApprovalWorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EscalateToEmployeeId",
                table: "ApprovalWorkflowSteps",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "FallbackEmployeeId",
                table: "ApprovalWorkflowSteps",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "OnUnresolved",
                table: "ApprovalWorkflowSteps",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "RemindAfterHours",
                table: "ApprovalWorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemindRepeatHours",
                table: "ApprovalWorkflowSteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttributesJson",
                table: "ApprovalInstances",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "EscalatedToEmployeeId",
                table: "ApprovalInstances",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReminderAt",
                table: "ApprovalInstances",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderCount",
                table: "ApprovalInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StepEnteredAt",
                table: "ApprovalInstances",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorEmployeeId",
                table: "ApprovalActions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "ApprovalActions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "OnBehalfOfEmployeeId",
                table: "ApprovalActions",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ApprovalDelegations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DelegatorEmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DelegateEmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ApplicationType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OnlyWhileOnLeave = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Employees_DelegateEmployeeId",
                        column: x => x.DelegateEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalDelegations_Employees_DelegatorEmployeeId",
                        column: x => x.DelegatorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ApprovalWorkflowStepGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApprovalWorkflowStepId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    GroupNumber = table.Column<int>(type: "int", nullable: false),
                    ApproverType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApproverEmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ApproverDepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ApproverPositionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ApproverLevel = table.Column<int>(type: "int", nullable: true),
                    OnUnresolved = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FallbackEmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    MinApprovals = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalWorkflowStepGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowStepGroups_ApprovalWorkflowSteps_ApprovalWor~",
                        column: x => x.ApprovalWorkflowStepId,
                        principalTable: "ApprovalWorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowStepGroups_Departments_ApproverDepartmentId",
                        column: x => x.ApproverDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApprovalWorkflowStepGroups_Positions_ApproverPositionId",
                        column: x => x.ApproverPositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalActions_OnBehalfOfEmployeeId",
                table: "ApprovalActions",
                column: "OnBehalfOfEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegateEmployeeId_IsActive",
                table: "ApprovalDelegations",
                columns: new[] { "DelegateEmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_DelegatorEmployeeId_IsActive",
                table: "ApprovalDelegations",
                columns: new[] { "DelegatorEmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDelegations_TenantId_DeletedAt",
                table: "ApprovalDelegations",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowStepGroups_ApprovalWorkflowStepId_GroupNumber",
                table: "ApprovalWorkflowStepGroups",
                columns: new[] { "ApprovalWorkflowStepId", "GroupNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowStepGroups_ApproverDepartmentId",
                table: "ApprovalWorkflowStepGroups",
                column: "ApproverDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowStepGroups_ApproverPositionId",
                table: "ApprovalWorkflowStepGroups",
                column: "ApproverPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalWorkflowStepGroups_TenantId_DeletedAt",
                table: "ApprovalWorkflowStepGroups",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ApprovalActions_Employees_OnBehalfOfEmployeeId",
                table: "ApprovalActions",
                column: "OnBehalfOfEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApprovalActions_Employees_OnBehalfOfEmployeeId",
                table: "ApprovalActions");

            migrationBuilder.DropTable(
                name: "ApprovalDelegations");

            migrationBuilder.DropTable(
                name: "ApprovalWorkflowStepGroups");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalActions_OnBehalfOfEmployeeId",
                table: "ApprovalActions");

            migrationBuilder.DropColumn(
                name: "HeadEmployeeId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "HeadEmployeeId",
                table: "Branches");

            migrationBuilder.DropColumn(
                name: "HeadEmployeeId",
                table: "Areas");

            migrationBuilder.DropColumn(
                name: "ApproverLevel",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ConditionMetric",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ConditionOperator",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ConditionValue",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "EscalateAfterHours",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "EscalateToEmployeeId",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "FallbackEmployeeId",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "OnUnresolved",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "RemindAfterHours",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "RemindRepeatHours",
                table: "ApprovalWorkflowSteps");

            migrationBuilder.DropColumn(
                name: "AttributesJson",
                table: "ApprovalInstances");

            migrationBuilder.DropColumn(
                name: "EscalatedToEmployeeId",
                table: "ApprovalInstances");

            migrationBuilder.DropColumn(
                name: "LastReminderAt",
                table: "ApprovalInstances");

            migrationBuilder.DropColumn(
                name: "ReminderCount",
                table: "ApprovalInstances");

            migrationBuilder.DropColumn(
                name: "StepEnteredAt",
                table: "ApprovalInstances");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "ApprovalActions");

            migrationBuilder.DropColumn(
                name: "OnBehalfOfEmployeeId",
                table: "ApprovalActions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActorEmployeeId",
                table: "ApprovalActions",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
