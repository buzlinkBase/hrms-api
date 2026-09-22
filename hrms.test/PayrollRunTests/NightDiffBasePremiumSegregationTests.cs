using Hrms.Core.Policies.DTRPolicies;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Covers the segregated Base/Premium recording added to OvertimeCategoryPolicies.cs/
/// NightDiffCategoryPolicies.cs/NightDiffOTCategoryPolicies.cs, built directly from the client's
/// own "Special Non Working (Base)" spreadsheet example: DailyRate 540 (HourlyRate 67.50), 3
/// Reg hours, 1 OT hour, 5 ND hours, 3 NDOT hours, SPECIAL_NON_WORKING=1.30, HOLIDAY_OT=1.25,
/// NIGHTDIFF=1.10. Confirms four things, all priced against ONE rate alone, never compounded
/// with another tier -- matching the client's spreadsheet exactly:
///  1. Existing blended Value (the actual paid amount) is completely unchanged by this feature.
///  2. FlatOvertimeBase (plain OT policy) = hours * rawOTRate * hourlyRate = 84.38 (the OT row).
///  3. NDBasePay (Value - NDPremium) = 438.75 (the ND row) -- day-type rate alone, no ND premium.
///  4. FlatOvertimeBase (NDOT policy) = hours * rawOTRate * hourlyRate = 253.13 (the NDOT row) --
///     the SAME raw OT rate as #2, applied to NDOT hours, ignoring day-type AND night premium.
///  5. The flat ND premiums -- FlatNightDiffPremium on both the ND and NDOT policies -- use ONLY
///     the (nightDiffRate - 1) fraction against the plain hourly rate, e.g. a 1.10 rate
///     contributes just .10, NOT compounded with any other tier. ND+NDOT flat ND premiums sum to
///     the spreadsheet's "Total ND Premium" of 54.00 exactly.
/// </summary>
public class NightDiffBasePremiumSegregationTests
{
    private const decimal DailyRate = 540m;
    private const double ShiftHours = 8;
    private const decimal HourlyRate = 67.5m; // DailyRate / ShiftHours

    private static PayrollContext CreateContext(double otHours = 0, double ndHours = 0, double ndotHours = 0, Guid? clientId = null)
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                DailyRate = DailyRate,
                ClientId = clientId,
                Settings = new EmployeeSettingModel
                {
                    IsEligibleForOvertime = true,
                    IsEligibleForNightDifferential = true,
                },
            },
            DailyRecord = new DailyRecordRunModel
            {
                ShiftWorkingHour = ShiftHours,
                SpecialHolOTHours = otHours,
                SpecialHolNightDiffHours = ndHours,
                SpecialHolNightDiffOTHours = ndotHours,
            },
            Payload = new CalculatorPayload(),
        };
    }

    private static void SetCompanyRate(PayrollContext context, RateType type, decimal rate)
        => context.Payload.PremiumRates[type] = rate;

    private static void SetClientRate(PayrollContext context, Guid clientId, RateType type, decimal rate)
        => context.Payload.ClientPremiumRates[new ClientRateKey(clientId, type)] = rate;

    [Fact]
    public void SpecialNonWorking_OTAndNDAndNDOT_MatchClientSpreadsheetExactly()
    {
        var context = CreateContext(otHours: 1, ndHours: 5, ndotHours: 3);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);

        var otLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var ndLine = new SpecialNonWorkingNDPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var ndotLine = new SpecialNonWorkingNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        // 1. Existing blended (actually paid) amounts -- untouched by this feature.
        otLine.Value.Should().Be(1 * HourlyRate * 1.30m * 1.25m); // 109.6875, compounded -- not the spreadsheet's row
        ndLine.Value.Should().Be(5 * HourlyRate * 1.30m * 1.10m); // 482.625
        ndotLine.Value.Should().Be(3 * HourlyRate * 1.30m * 1.25m * 1.10m); // 361.96875

        // 2. OT Base -- raw OT rate alone, matches the spreadsheet's OT row exactly.
        otLine.FlatOvertimeBase.Should().Be(84.375m); // 1 x 1.25 x 67.5, displays as 84.38

        // 3. ND Base -- day-type tier only, no night premium. Matches the ND row exactly.
        var ndBase = ndLine.Value - ndLine.NDPremium;
        ndBase.Should().Be(438.75m);

        // 4. NDOT Base -- the SAME raw OT rate as #2, applied to NDOT hours. Matches the NDOT
        // row exactly (253.13), NOT the compounded day-type x OT amount (329.0625).
        ndotLine.FlatOvertimeBase.Should().Be(253.125m);

        // 5. Flat ND premiums -- (rate - 1) against the plain hourly rate only.
        ndLine.FlatNightDiffPremium.Should().Be(5 * HourlyRate * 0.10m); // 33.75
        ndotLine.FlatNightDiffPremium.Should().Be(3 * HourlyRate * 0.10m); // 20.25

        // Matches the spreadsheet's "Total ND Premium" exactly.
        (ndLine.FlatNightDiffPremium + ndotLine.FlatNightDiffPremium).Should().Be(54.00m);
    }

    [Fact]
    public void FlatNightDiffPremium_ScalesWithConfiguredRate_ExcludingTheBaseOne()
    {
        // Per the client's own framing: "if we set NightDiff rate to 1.15 we only use the .15".
        var context = CreateContext(ndHours: 5);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.15m);

        var ndLine = new SpecialNonWorkingNDPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        ndLine.FlatNightDiffPremium.Should().Be(5 * HourlyRate * 0.15m);
    }

    [Fact]
    public void FlatOvertimeBase_HonorsClientPremiumOverride_SameRateAsActualPay()
    {
        // A client override on SPECIAL_HOLIDAY_OT_PREMIUM changes both the actual paid OT rate
        // (Value, compounded with the day rate) AND the segregated FlatOvertimeBase (OT tier
        // alone) -- both must trace back to the exact same configured premium, not diverge
        // depending on which one a caller asks for.
        var clientId = Guid.NewGuid();
        var context = CreateContext(otHours: 1, clientId: clientId);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m); // company default -- not what this client actually gets
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT_PREMIUM, 1.50m); // this client's negotiated premium

        var otLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        otLine.Value.Should().Be(1 * HourlyRate * (1.30m * 1.50m)); // actual pay: day rate x this client's premium
        otLine.FlatOvertimeBase.Should().Be(1 * HourlyRate * 1.50m); // Base: the same premium alone, day-rate excluded
    }

    [Fact]
    public void ZeroHours_ContributesNothingToBaseOrPremium()
    {
        var context = CreateContext(otHours: 0, ndHours: 0, ndotHours: 0);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);

        var otLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var ndLine = new SpecialNonWorkingNDPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var ndotLine = new SpecialNonWorkingNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        otLine.FlatOvertimeBase.Should().Be(0m);
        ndLine.FlatNightDiffPremium.Should().Be(0m);
        ndotLine.FlatOvertimeBase.Should().Be(0m);
        ndotLine.FlatNightDiffPremium.Should().Be(0m);
    }

    [Fact]
    public void NothingConfigured_DayTypeAndRateFallbacksUseRateDefaultConstants_NotBlanket1Point0Or1Point25()
    {
        // Regression guard, same class of bug as OvertimeRateStrategiesTests' equivalent test --
        // NightDiffCategoryPolicies.cs/NightDiffOTCategoryPolicies.cs had the identical blanket
        // 1.0m/1.25m fallback bug, independently of OvertimeRateStrategies.cs.
        var context = CreateContext(otHours: 1, ndHours: 5, ndotHours: 3);
        // No company/client rates configured at all -- every lookup must fall through to
        // RATE_DEFAULT.For(...): SPECIAL_NON_WORKING=1.30, HOLIDAY_OT=1.30, NIGHTDIFF=1.10.

        var otLine = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var ndLine = new SpecialNonWorkingNDPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);
        var ndotLine = new SpecialNonWorkingNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        otLine.FlatOvertimeBase.Should().Be(1 * HourlyRate * 1.30m); // RATE_DEFAULT.HOLIDAY_OT, not .OVERTIME's 1.25
        var ndBase = ndLine.Value - ndLine.NDPremium;
        ndBase.Should().Be(5 * HourlyRate * 1.30m); // RATE_DEFAULT.SPECIAL_NON_WORKING, not a blanket 1.0
        ndotLine.FlatOvertimeBase.Should().Be(3 * HourlyRate * 1.30m); // RATE_DEFAULT.HOLIDAY_OT, not .OVERTIME's 1.25
    }

    [Fact]
    public void NDOTPolicy_ActualPayHonorsClientPremiumOverride_SameAsPlainOTPolicy()
    {
        // Regression guard for the real payroll-calculation bug (not just presentation):
        // NightDiffOTCategoryPolicies used to resolve its OT-tier rate via a plain
        // PremiumRateHelper.GetRate(HOLIDAY_OT) lookup, bypassing any client override entirely --
        // a client's negotiated OT premium was silently ignored for any hour that was ALSO at
        // night, even though the same client's plain-OT hours (SingleCategoryOTPolicy) already
        // correctly honored it. Both must now trace back to the same configured premium.
        var clientId = Guid.NewGuid();
        var context = CreateContext(ndotHours: 3, clientId: clientId);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m); // the raw default this bug used to fall back to
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);
        SetClientRate(context, clientId, RateType.SPECIAL_HOLIDAY_OT_PREMIUM, 1.50m); // this client's negotiated premium

        var ndotLine = new SpecialNonWorkingNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        // day rate (1.30) x this client's premium (1.50) x night rate (1.10) = 2.145.
        var expectedFullRate = 1.30m * 1.50m * 1.10m;
        ndotLine.Value.Should().Be(3 * HourlyRate * expectedFullRate);
        // Would have been 3 x HourlyRate x (1.30 x 1.25 x 1.10) before the fix -- confirms this
        // isn't silently falling back to the raw company-wide HOLIDAY_OT default.
        ndotLine.Value.Should().NotBe(3 * HourlyRate * (1.30m * 1.25m * 1.10m));
    }
}
