using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class roster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "GeneralSettings",
                columns: new[] { "Id", "CreatedAt", "DeletedAt", "Description", "IdentityType", "IdentityTypeId", "Metadata", "Status", "TenantId", "UpdatedAt", "Value" },
                values: new object[] { new Guid("6789f0a1-b2c3-489a-defa-2345678901fa"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "TreatNdotAsNdOnly", "Company", null, null, "Active", new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "False" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GeneralSettings",
                keyColumn: "Id",
                keyValue: new Guid("6789f0a1-b2c3-489a-defa-2345678901fa"));
        }
    }
}
