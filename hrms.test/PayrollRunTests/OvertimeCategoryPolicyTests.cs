using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// End-to-end wiring tests for the 7 holiday/rest-day OT policies (Hrms.Core/Policies/
/// DTRPolicies/OvertimeCategoryPolicies.cs) — proves SingleCategoryOTPolicy's Template Method
/// correctly composes with each policy's IOtRateStrategy (CompoundedOtRateStrategy, whose
/// otType/otFallbackType let a client override a category's own OT PREMIUM, falling back to the
/// shared HOLIDAY_OT when not overridden), not just the strategies in isolation
/// (OvertimeRateStrategiesTests). Covers the three concrete scenarios from the client's
/// "Holiday Overtime Computation Per Detachment" spreadsheet.
///
/// These client overrides used to be flat TOTAL replacements (LEGAL_HOLIDAY_OT/SPECIAL_HOLIDAY_OT,
/// entirely decoupled from the day-type rate). They're now per-category PREMIUMS that compound
/// with the day-type rate instead, so an old flat total of e.g. 1.25 is reconfigured as
/// 1.25 / dayRate. Where that division is clean (a multiple of the day rate), the exact original
/// spreadsheet peso amount still reconciles exactly. Where it isn't (documented per-test), the
/// new model can only approximate the old flat total to within a fraction of a centavo — an
/// accepted, deliberate tradeoff of moving to a per-day-type premium model instead of a flat
/// override; see this session's plan notes.
/// </summary>
public class OvertimeCategoryPolicyTests
{
    private const decimal DailyRate = 800m;
    private const double ShiftHours = 8;
    private const decimal HourlyRate = 100m; // DailyRate / ShiftHours

    private static PayrollContext CreateContext(Guid? clientId, double legalHolOtHours = 0, double specialHolOtHours = 0, decimal? dailyRate = null)
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                ClientId = clientId,
                DailyRate = dailyRate ?? DailyRate,
                Settings = new EmployeeSettingModel { IsEligibleForOvertime = true },
            },
            DailyRecord = new DailyRecordRunModel
            {
                ShiftWorkingHour = ShiftHours,
                LegalHolOTHours = legalHolOtHours,
                SpecialHolOTHours = specialHolOtHours,
            },
            Payload = new CalculatorPayload(),
        };
    }

    private static void SetCompanyRate(PayrollContext context, RateType type, decimal rate)
        => context.Payload.PremiumRates[type] = rate;

    private static void SetClientRate(PayrollContext context, Guid clientId, RateType type, decimal rate)
        => context.Payload.ClientPremiumRates[new ClientRateKey(clientId, type)] = rate;

    [Fact]
    public void RadissonBlu_NoOverrideConfigured_MatchesSpreadsheetViaStandardFormulaAlone()
    {
        var context = CreateContext(clientId: Guid.NewGuid(), legalHolOtHours: 2, specialHolOtHours: 2);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(HourlyRate * 2 * 2.60m);
        specialLine.Value.Should().Be(HourlyRate * 2 * 1.69m);
    }

    [Fact]
    public void StandardFlatGroup_ClientOverrideConfigured_BothCategoriesLandOnOnePointTwoFive()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 3, specialHolOtHours: 3);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        // Old flat totals were both 1.25 -- Legal's day rate (2.00) divides it cleanly (0.625),
        // Special's (1.30) doesn't, so its premium is configured as the closest representable
        // approximation instead (see class doc comment).
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.25m / 2.00m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT_PREMIUM, 1.25m / 1.30m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(HourlyRate * 3 * 1.25m); // 0.625 x 2.00 recombines exactly
        specialLine.Value.Should().Be(HourlyRate * 3 * (1.30m * (1.25m / 1.30m))); // ~337.50, within a fraction of a centavo
        // OTPremium is honestly negative here -- this client's negotiated OT rate (1.25) is
        // below their own standard day rate (2.00/1.30) -- see the plan's reporting caveat.
        legalLine.OTPremium.Should().Be(HourlyRate * 3 * (1.25m - 2.00m));
    }

    [Fact]
    public void MetroRetailGroup_ClientOverrideConfigured_MatchesSpreadsheetsAdditiveFormulasExactly()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 4, specialHolOtHours: 4);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        // Both old flat totals (2.25, 1.625) divide their day rates (2.00, 1.30) cleanly --
        // 1.125 and 1.25 respectively -- so the exact spreadsheet totals still reconcile exactly.
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.125m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT_PREMIUM, 1.25m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        // Spreadsheet: rate/8*1.25*hrs + rate/8*hrs == rate/8*hrs*2.25
        legalLine.Value.Should().Be(HourlyRate * 4 * 1.25m + HourlyRate * 4);
        // Spreadsheet: rate/8*1.25*hrs + (rate/8*1.25*hrs)*30% == rate/8*hrs*1.625
        specialLine.Value.Should().Be(HourlyRate * 4 * 1.25m + (HourlyRate * 4 * 1.25m) * 0.30m);
    }

    [Fact]
    public void OverrideForOneClient_DoesNotAffectAnotherClientWithNoOverride()
    {
        var overriddenClientId = Guid.NewGuid();
        var plainClientId = Guid.NewGuid();

        var overriddenContext = CreateContext(overriddenClientId, legalHolOtHours: 1);
        SetCompanyRate(overriddenContext, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(overriddenContext, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(overriddenContext, overriddenClientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.25m / 2.00m);

        var plainContext = CreateContext(plainClientId, legalHolOtHours: 1);
        plainContext.Payload = overriddenContext.Payload; // share the same company/client rate dictionaries
        SetCompanyRate(plainContext, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(plainContext, RateType.HOLIDAY_OT, 1.30m);

        var overriddenLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), overriddenContext);
        var plainLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), plainContext);

        overriddenLine.Value.Should().Be(HourlyRate * 1 * 1.25m);
        plainLine.Value.Should().Be(HourlyRate * 1 * 2.60m); // unaffected by the other client's override -- falls back to shared HOLIDAY_OT
    }

    [Fact]
    public void UnsetPremium_FallsBackToSharedHolidayOt_NotAFlatDefault()
    {
        // Regression guard for the new fallback chain: a client override on HOLIDAY_OT itself
        // (not the per-category premium) must still be picked up when the category's own
        // premium is left blank -- proves the fallback goes through HOLIDAY_OT's own full
        // client-or-company resolution, not a hardcoded RATE_DEFAULT constant.
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 1);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m); // company default
        SetClientRate(context, clientId, RateType.HOLIDAY_OT, 1.40m); // this client's own HOLIDAY_OT override
        // No LEGAL_HOLIDAY_OT_PREMIUM override configured for this client.

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(HourlyRate * 1 * (2.00m * 1.40m)); // the client's own HOLIDAY_OT, not the company's 1.30
    }

    [Fact]
    public void ZeroHours_ContributesNothing_RegardlessOfOverride()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 0);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 0.625m);

        var line = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(0m);
    }

    [Fact]
    public void NotEligibleForOvertime_ContributesNothing_RegardlessOfOverride()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 4);
        context.Employee.Settings = new EmployeeSettingModel { IsEligibleForOvertime = false };
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 0.625m);

        var line = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(0m);
    }

    // Below: acceptance tests built directly from the client's two real spreadsheets
    // ("Rate Per Detachment" + "Holiday Overtime Computation Per Detachment"), rather than the
    // synthetic DailyRate=800 used above. Every Sample in the client's sheet is computed at
    // exactly 4 OT hours, so all cases here fix legalHolOtHours/specialHolOtHours at 4 and vary
    // only the per-detachment Rate. Confirms real production rates, not just round numbers,
    // reconcile through CompoundedOtRateStrategy to the cent (or as close as a per-day-type
    // premium can get -- see class doc comment).
    //
    // Radisson Blu note: "Rate Per Detachment" splits RAD into two rates -- Detachment
    // Commander=594.00 and Security Guards=540.00 -- but "Holiday Overtime Computation Per
    // Detachment" has only one RAD row, whose Sample (702.00 / 456.30) reconciles against the
    // Security Guards rate (540), not the Commander rate (594). Flagging this as a real
    // discrepancy worth confirming with the client/HR rather than silently picking one.
    [Fact]
    public void RadissonBlu_SecurityGuardsRate540_MatchesSpreadsheetSample()
    {
        var context = CreateContext(clientId: Guid.NewGuid(), legalHolOtHours: 4, specialHolOtHours: 4, dailyRate: 540m);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(702.00m);
        specialLine.Value.Should().Be(456.30m);
    }

    // Standard-override group (client override LEGAL_HOLIDAY_OT_PREMIUM=1.25/2.00=0.625 /
    // SPECIAL_HOLIDAY_OT_PREMIUM=1.25/1.30), three distinct rate tiers actually present in "Rate
    // Per Detachment": 435 (Density Steel), 470 (Glacier Samar / Christ The King / B. Vicencio /
    // St. Camillus / SOS), and 540 (Cebu Pacific / Municipality of Cordova / Balai Punta Engano /
    // Singapore Cancer Center / Gothong Cargo / General Milling x3 / Waterfront Cebu).
    [Theory]
    [InlineData(435, 271.875)] // Density Steel
    [InlineData(470, 293.75)] // Glacier Samar / Christ The King / B. Vicencio / St. Camillus / SOS
    [InlineData(540, 337.50)] // Cebu Pacific / Cordova / Balai / Singapore / Gothong / General Milling x3 / Waterfront
    public void StandardOverrideGroup_RealDetachmentRates_MatchSpreadsheetSamples(decimal rate, decimal expected)
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 4, specialHolOtHours: 4, dailyRate: rate);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.25m / 2.00m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT_PREMIUM, 1.25m / 1.30m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        var hourlyRate = rate / 8m;
        var basePayForHours = hourlyRate * 4m;

        legalLine.Value.Should().Be(expected); // 0.625 x 2.00 recombines to exactly 1.25 -- unaffected
        // Special's day rate (1.30) doesn't divide 1.25 cleanly -- asserted against the exact
        // same expression the production code evaluates, not the old flat-override spreadsheet
        // literal, since the two now differ by a fraction of a centavo (accepted tradeoff).
        specialLine.Value.Should().Be(basePayForHours * (1.30m * (1.25m / 1.30m)));
    }

    // Metro Retail Stores Group / Beneluxe Trading (18 detachments, all Rate=540): client
    // override LEGAL_HOLIDAY_OT_PREMIUM=1.125 (=2.25/2.00) / SPECIAL_HOLIDAY_OT_PREMIUM=1.25
    // (=1.625/1.30), the additive-formula group -- both divide cleanly, so the exact spreadsheet
    // totals still reconcile exactly.
    [Fact]
    public void MetroRetailBeneluxeGroup_RealDetachmentRate540_MatchesSpreadsheetSample()
    {
        var clientId = Guid.NewGuid();
        var context = CreateContext(clientId, legalHolOtHours: 4, specialHolOtHours: 4, dailyRate: 540m);
        SetCompanyRate(context, RateType.LEGAL_HOLIDAY_DUTY, 2.00m);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.30m);
        SetClientRate(context, clientId, RateType.LEGAL_HOLIDAY_OT_PREMIUM, 1.125m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT_PREMIUM, 1.25m);

        var legalLine = new LegalHolOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var specialLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        legalLine.Value.Should().Be(607.50m);
        specialLine.Value.Should().Be(438.75m);
    }
}
