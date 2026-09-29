using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class scheduled_notif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Body = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiresAcknowledgement = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AcknowledgeByUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PublishAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsPinned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PinnedUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExcludeInactive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    State = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StateBeforeArchive = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StateBeforeDelete = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CreatedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PublishedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnpublishedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UnpublishedByName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UnpublishReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BinnedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    LastEditedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AckRequiredVersion = table.Column<int>(type: "int", nullable: false),
                    PublishJobToken = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ExpireJobToken = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PurgeJobToken = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AnnouncementAudienceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AnnouncementId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Kind = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TargetValue = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetLabel = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsExclusion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementAudienceRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementAudienceRules_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AnnouncementRecipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AnnouncementId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeNo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DepartmentName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PositionName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BranchName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FirstViewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastViewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AcknowledgedVersion = table.Column<int>(type: "int", nullable: true),
                    LastRemindedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RemindCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementRecipients_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAudienceRules_AnnouncementId",
                table: "AnnouncementAudienceRules",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementAudienceRules_TenantId_DeletedAt",
                table: "AnnouncementAudienceRules",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementRecipients_AnnouncementId_EmployeeId",
                table: "AnnouncementRecipients",
                columns: new[] { "AnnouncementId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementRecipients_EmployeeId_FirstViewedAt",
                table: "AnnouncementRecipients",
                columns: new[] { "EmployeeId", "FirstViewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementRecipients_TenantId_DeletedAt",
                table: "AnnouncementRecipients",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_State_BinnedAtUtc",
                table: "Announcements",
                columns: new[] { "State", "BinnedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_State_ExpiresAtUtc",
                table: "Announcements",
                columns: new[] { "State", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_State_PublishAtUtc",
                table: "Announcements",
                columns: new[] { "State", "PublishAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_TenantId_DeletedAt",
                table: "Announcements",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnouncementAudienceRules");

            migrationBuilder.DropTable(
                name: "AnnouncementRecipients");

            migrationBuilder.DropTable(
                name: "Announcements");
        }
    }
}
