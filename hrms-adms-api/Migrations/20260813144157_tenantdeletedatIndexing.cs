using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.adms.Migrations
{
    /// <inheritdoc />
    public partial class tenantdeletedatIndexing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ThermalAndMaskConfigs_TenantId_DeletedAt",
                table: "ThermalAndMaskConfigs",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemCounters_TenantId_DeletedAt",
                table: "SystemCounters",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QrCodeConfigs_TenantId_DeletedAt",
                table: "QrCodeConfigs",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotosAndMedias_TenantId_DeletedAt",
                table: "PhotosAndMedias",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MultiBioSupports_TenantId_DeletedAt",
                table: "MultiBioSupports",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeaturesAndProtocols_TenantId_DeletedAt",
                table: "FeaturesAndProtocols",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUsers_TenantId_DeletedAt",
                table: "DeviceUsers",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCommands_TenantId_DeletedAt",
                table: "DeviceCommands",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricTemplates_TenantId_DeletedAt",
                table: "BiometricTemplates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Biometrics_TenantId_DeletedAt",
                table: "Biometrics",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricDevices_TenantId_DeletedAt",
                table: "BiometricDevices",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BiometricDetails_TenantId_DeletedAt",
                table: "BiometricDetails",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_TenantId_DeletedAt",
                table: "Attendances",
                columns: new[] { "TenantId", "DeletedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ThermalAndMaskConfigs_TenantId_DeletedAt",
                table: "ThermalAndMaskConfigs");

            migrationBuilder.DropIndex(
                name: "IX_SystemCounters_TenantId_DeletedAt",
                table: "SystemCounters");

            migrationBuilder.DropIndex(
                name: "IX_QrCodeConfigs_TenantId_DeletedAt",
                table: "QrCodeConfigs");

            migrationBuilder.DropIndex(
                name: "IX_PhotosAndMedias_TenantId_DeletedAt",
                table: "PhotosAndMedias");

            migrationBuilder.DropIndex(
                name: "IX_MultiBioSupports_TenantId_DeletedAt",
                table: "MultiBioSupports");

            migrationBuilder.DropIndex(
                name: "IX_FeaturesAndProtocols_TenantId_DeletedAt",
                table: "FeaturesAndProtocols");

            migrationBuilder.DropIndex(
                name: "IX_DeviceUsers_TenantId_DeletedAt",
                table: "DeviceUsers");

            migrationBuilder.DropIndex(
                name: "IX_DeviceCommands_TenantId_DeletedAt",
                table: "DeviceCommands");

            migrationBuilder.DropIndex(
                name: "IX_BiometricTemplates_TenantId_DeletedAt",
                table: "BiometricTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Biometrics_TenantId_DeletedAt",
                table: "Biometrics");

            migrationBuilder.DropIndex(
                name: "IX_BiometricDevices_TenantId_DeletedAt",
                table: "BiometricDevices");

            migrationBuilder.DropIndex(
                name: "IX_BiometricDetails_TenantId_DeletedAt",
                table: "BiometricDetails");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_TenantId_DeletedAt",
                table: "Attendances");
        }
    }
}
