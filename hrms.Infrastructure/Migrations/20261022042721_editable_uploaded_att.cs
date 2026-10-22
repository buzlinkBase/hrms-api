using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class editable_uploaded_att : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceOriginalLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Action = table.Column<int>(type: "int", nullable: false),
                    AttendanceId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BioId = table.Column<int>(type: "int", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    WorkDateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LogSource = table.Column<int>(type: "int", nullable: false),
                    BatchCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IP = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BranchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OperationAreaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UserName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Workstate = table.Column<int>(type: "int", nullable: false),
                    Verifycode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogRemarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EditRemarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Boundary = table.Column<Polygon>(type: "geometry", nullable: true)
                        .Annotation("MySql:SpatialReferenceSystemId", 4326),
                    Reason = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ActedByName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceOriginalLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceOriginalLog_AttendanceId",
                table: "AttendanceOriginalLogs",
                column: "AttendanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceOriginalLog_BatchCode_LogSource",
                table: "AttendanceOriginalLogs",
                columns: new[] { "BatchCode", "LogSource" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceOriginalLog_BioId_WorkDateTime",
                table: "AttendanceOriginalLogs",
                columns: new[] { "BioId", "WorkDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceOriginalLogs_TenantId_DeletedAt",
                table: "AttendanceOriginalLogs",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceOriginalLogs");
        }
    }
}
