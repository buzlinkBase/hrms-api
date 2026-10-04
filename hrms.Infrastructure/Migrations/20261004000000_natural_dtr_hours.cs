using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <summary>
    /// Data only (no schema change): moves saved DTR and payroll hours to the natural layout the
    /// DTR now produces (dtr-api DailyRecordBuilder). Before, Hrs and OT had their night hours
    /// taken out, ND held only the night regular hours, and NDOT the night OT hours. Now:
    ///   Hrs = Hrs + ND      (all regular hours, night included)
    ///   OT  = OT + NDOT     (all OT hours, night included)
    ///   ND  = ND + NDOT     (all night hours)
    ///   NDOT unchanged      (the night-OT subset of OT and ND)
    /// Payrolls.OvertimeHours (the sum of the OT columns) gains the NDOT hours too. Pay amounts
    /// are untouched — the pay calculators separate the natural hours again (CategoryHours.Separate).
    /// MySQL applies a single-table UPDATE's assignments left to right, each seeing the ones
    /// before it, so every category's Hrs is set before its ND (Up) and its ND before its Hrs (Down).
    /// </summary>
    [DbContext(typeof(HrmsContext))]
    [Migration("20261004000000_natural_dtr_hours")]
    public partial class natural_dtr_hours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE `DailyTimeRecords` SET
    `RegularNetHours` = `RegularNetHours` + `RegularNDHours`,
    `RegularOTHours` = `RegularOTHours` + `RegularNDOTHours`,
    `RegularNDHours` = `RegularNDHours` + `RegularNDOTHours`,
    `RestDayHours` = `RestDayHours` + `RestDayNDHours`,
    `RestDayOTHours` = `RestDayOTHours` + `RestDayNDOTHours`,
    `RestDayNDHours` = `RestDayNDHours` + `RestDayNDOTHours`,
    `LegalHolHours` = `LegalHolHours` + `LegalHolNightDiffHours`,
    `LegalHolOTHours` = `LegalHolOTHours` + `LegalHolNightDiffOTHours`,
    `LegalHolNightDiffHours` = `LegalHolNightDiffHours` + `LegalHolNightDiffOTHours`,
    `SpecialHolHours` = `SpecialHolHours` + `SpecialHolNightDiffHours`,
    `SpecialHolOTHours` = `SpecialHolOTHours` + `SpecialHolNightDiffOTHours`,
    `SpecialHolNightDiffHours` = `SpecialHolNightDiffHours` + `SpecialHolNightDiffOTHours`,
    `RestLegalDayHours` = `RestLegalDayHours` + `RestLegalDayNDHours`,
    `RestLegalDayOTHours` = `RestLegalDayOTHours` + `RestLegalDayNDOTHours`,
    `RestLegalDayNDHours` = `RestLegalDayNDHours` + `RestLegalDayNDOTHours`,
    `RestSpecialDayHours` = `RestSpecialDayHours` + `RestSpecialDayNDHours`,
    `RestSpecialDayOTHours` = `RestSpecialDayOTHours` + `RestSpecialDayNDOTHours`,
    `RestSpecialDayNDHours` = `RestSpecialDayNDHours` + `RestSpecialDayNDOTHours`,
    `DoubleLegalHours` = `DoubleLegalHours` + `DoubleLegalNDHours`,
    `DoubleLegalOTHours` = `DoubleLegalOTHours` + `DoubleLegalNDOTHours`,
    `DoubleLegalNDHours` = `DoubleLegalNDHours` + `DoubleLegalNDOTHours`,
    `RestDoubleLegalHours` = `RestDoubleLegalHours` + `RestDoubleLegalNDHours`,
    `RestDoubleLegalOTHours` = `RestDoubleLegalOTHours` + `RestDoubleLegalNDOTHours`,
    `RestDoubleLegalNDHours` = `RestDoubleLegalNDHours` + `RestDoubleLegalNDOTHours`;");

            migrationBuilder.Sql(@"
UPDATE `Payrolls` SET
    `RegularNetHours` = `RegularNetHours` + `RegularNDHours`,
    `RegularOTHours` = `RegularOTHours` + `RegularNDOTHours`,
    `RegularNDHours` = `RegularNDHours` + `RegularNDOTHours`,
    `RestDayHours` = `RestDayHours` + `RestDayNDHours`,
    `RestDayOTHours` = `RestDayOTHours` + `RestDayNDOTHours`,
    `RestDayNDHours` = `RestDayNDHours` + `RestDayNDOTHours`,
    `LegalHolHours` = `LegalHolHours` + `LegalHolNightDiffHours`,
    `LegalHolOTHours` = `LegalHolOTHours` + `LegalHolNightDiffOTHours`,
    `LegalHolNightDiffHours` = `LegalHolNightDiffHours` + `LegalHolNightDiffOTHours`,
    `SpecialHolHours` = `SpecialHolHours` + `SpecialHolNightDiffHours`,
    `SpecialHolOTHours` = `SpecialHolOTHours` + `SpecialHolNightDiffOTHours`,
    `SpecialHolNightDiffHours` = `SpecialHolNightDiffHours` + `SpecialHolNightDiffOTHours`,
    `RestLegalDayHours` = `RestLegalDayHours` + `RestLegalDayNDHours`,
    `RestLegalDayOTHours` = `RestLegalDayOTHours` + `RestLegalDayNDOTHours`,
    `RestLegalDayNDHours` = `RestLegalDayNDHours` + `RestLegalDayNDOTHours`,
    `RestSpecialDayHours` = `RestSpecialDayHours` + `RestSpecialDayNDHours`,
    `RestSpecialDayOTHours` = `RestSpecialDayOTHours` + `RestSpecialDayNDOTHours`,
    `RestSpecialDayNDHours` = `RestSpecialDayNDHours` + `RestSpecialDayNDOTHours`,
    `DoubleLegalHours` = `DoubleLegalHours` + `DoubleLegalNDHours`,
    `DoubleLegalOTHours` = `DoubleLegalOTHours` + `DoubleLegalNDOTHours`,
    `DoubleLegalNDHours` = `DoubleLegalNDHours` + `DoubleLegalNDOTHours`,
    `RestDoubleLegalHours` = `RestDoubleLegalHours` + `RestDoubleLegalNDHours`,
    `RestDoubleLegalOTHours` = `RestDoubleLegalOTHours` + `RestDoubleLegalNDOTHours`,
    `RestDoubleLegalNDHours` = `RestDoubleLegalNDHours` + `RestDoubleLegalNDOTHours`,
    `OvertimeHours` = `OvertimeHours` + `RegularNDOTHours` + `RestDayNDOTHours` + `LegalHolNightDiffOTHours` + `SpecialHolNightDiffOTHours` + `RestLegalDayNDOTHours` + `RestSpecialDayNDOTHours` + `DoubleLegalNDOTHours` + `RestDoubleLegalNDOTHours`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE `DailyTimeRecords` SET
    `RegularNDHours` = `RegularNDHours` - `RegularNDOTHours`,
    `RegularNetHours` = `RegularNetHours` - `RegularNDHours`,
    `RegularOTHours` = `RegularOTHours` - `RegularNDOTHours`,
    `RestDayNDHours` = `RestDayNDHours` - `RestDayNDOTHours`,
    `RestDayHours` = `RestDayHours` - `RestDayNDHours`,
    `RestDayOTHours` = `RestDayOTHours` - `RestDayNDOTHours`,
    `LegalHolNightDiffHours` = `LegalHolNightDiffHours` - `LegalHolNightDiffOTHours`,
    `LegalHolHours` = `LegalHolHours` - `LegalHolNightDiffHours`,
    `LegalHolOTHours` = `LegalHolOTHours` - `LegalHolNightDiffOTHours`,
    `SpecialHolNightDiffHours` = `SpecialHolNightDiffHours` - `SpecialHolNightDiffOTHours`,
    `SpecialHolHours` = `SpecialHolHours` - `SpecialHolNightDiffHours`,
    `SpecialHolOTHours` = `SpecialHolOTHours` - `SpecialHolNightDiffOTHours`,
    `RestLegalDayNDHours` = `RestLegalDayNDHours` - `RestLegalDayNDOTHours`,
    `RestLegalDayHours` = `RestLegalDayHours` - `RestLegalDayNDHours`,
    `RestLegalDayOTHours` = `RestLegalDayOTHours` - `RestLegalDayNDOTHours`,
    `RestSpecialDayNDHours` = `RestSpecialDayNDHours` - `RestSpecialDayNDOTHours`,
    `RestSpecialDayHours` = `RestSpecialDayHours` - `RestSpecialDayNDHours`,
    `RestSpecialDayOTHours` = `RestSpecialDayOTHours` - `RestSpecialDayNDOTHours`,
    `DoubleLegalNDHours` = `DoubleLegalNDHours` - `DoubleLegalNDOTHours`,
    `DoubleLegalHours` = `DoubleLegalHours` - `DoubleLegalNDHours`,
    `DoubleLegalOTHours` = `DoubleLegalOTHours` - `DoubleLegalNDOTHours`,
    `RestDoubleLegalNDHours` = `RestDoubleLegalNDHours` - `RestDoubleLegalNDOTHours`,
    `RestDoubleLegalHours` = `RestDoubleLegalHours` - `RestDoubleLegalNDHours`,
    `RestDoubleLegalOTHours` = `RestDoubleLegalOTHours` - `RestDoubleLegalNDOTHours`;");

            migrationBuilder.Sql(@"
UPDATE `Payrolls` SET
    `RegularNDHours` = `RegularNDHours` - `RegularNDOTHours`,
    `RegularNetHours` = `RegularNetHours` - `RegularNDHours`,
    `RegularOTHours` = `RegularOTHours` - `RegularNDOTHours`,
    `RestDayNDHours` = `RestDayNDHours` - `RestDayNDOTHours`,
    `RestDayHours` = `RestDayHours` - `RestDayNDHours`,
    `RestDayOTHours` = `RestDayOTHours` - `RestDayNDOTHours`,
    `LegalHolNightDiffHours` = `LegalHolNightDiffHours` - `LegalHolNightDiffOTHours`,
    `LegalHolHours` = `LegalHolHours` - `LegalHolNightDiffHours`,
    `LegalHolOTHours` = `LegalHolOTHours` - `LegalHolNightDiffOTHours`,
    `SpecialHolNightDiffHours` = `SpecialHolNightDiffHours` - `SpecialHolNightDiffOTHours`,
    `SpecialHolHours` = `SpecialHolHours` - `SpecialHolNightDiffHours`,
    `SpecialHolOTHours` = `SpecialHolOTHours` - `SpecialHolNightDiffOTHours`,
    `RestLegalDayNDHours` = `RestLegalDayNDHours` - `RestLegalDayNDOTHours`,
    `RestLegalDayHours` = `RestLegalDayHours` - `RestLegalDayNDHours`,
    `RestLegalDayOTHours` = `RestLegalDayOTHours` - `RestLegalDayNDOTHours`,
    `RestSpecialDayNDHours` = `RestSpecialDayNDHours` - `RestSpecialDayNDOTHours`,
    `RestSpecialDayHours` = `RestSpecialDayHours` - `RestSpecialDayNDHours`,
    `RestSpecialDayOTHours` = `RestSpecialDayOTHours` - `RestSpecialDayNDOTHours`,
    `DoubleLegalNDHours` = `DoubleLegalNDHours` - `DoubleLegalNDOTHours`,
    `DoubleLegalHours` = `DoubleLegalHours` - `DoubleLegalNDHours`,
    `DoubleLegalOTHours` = `DoubleLegalOTHours` - `DoubleLegalNDOTHours`,
    `RestDoubleLegalNDHours` = `RestDoubleLegalNDHours` - `RestDoubleLegalNDOTHours`,
    `RestDoubleLegalHours` = `RestDoubleLegalHours` - `RestDoubleLegalNDHours`,
    `RestDoubleLegalOTHours` = `RestDoubleLegalOTHours` - `RestDoubleLegalNDOTHours`,
    `OvertimeHours` = `OvertimeHours` - (`RegularNDOTHours` + `RestDayNDOTHours` + `LegalHolNightDiffOTHours` + `SpecialHolNightDiffOTHours` + `RestLegalDayNDOTHours` + `RestSpecialDayNDOTHours` + `DoubleLegalNDOTHours` + `RestDoubleLegalNDOTHours`);");
        }
    }
}
