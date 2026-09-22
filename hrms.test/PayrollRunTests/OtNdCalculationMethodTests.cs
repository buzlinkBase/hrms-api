using Hrms.Core.Policies.DTRPolicies;
using Hrms.Domain.ValueObjects;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Setup > Company Policy > OT/ND Calculation Method — Compounded (default, DOLE-standard:
/// dayRate x otRate (x ndRate for NDOT)) vs Additive (opt-in: the day-type multiplier is DROPPED
/// entirely for OT-involving hours — only the raw OT rate applies, plus (ndRate - 1) for NDOT).
/// Confirmed against the company's own manual payroll worksheet. Computes LESS than Compounded
/// for any category whose day rate isn't 1.0. Plain ND-only hours (no OT) are unaffected by this
/// setting in either mode — they always keep the day-type rate. For Regular category (dayRate ==
/// 1.0), dropping the day rate has no numeric effect vs. keeping it (multiplying/adding 1 is a
/// no-op either way), so Regular's OT-only and NDOT hours happen to be identical in both modes —
/// see the two Regular-category tests below.
/// </summary>
public class OtNdCalculationMethodTests
{
    private const decimal HourlyRate = 67.5m; // DailyRate 540 / 8

    private static PayrollContext CreateContext(OtNdCalculationMethod method, double otHours = 0, double ndHours = 0, double ndotHours = 0)
    {
        return new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                DailyRate = 540m,
                Settings = new EmployeeSettingModel
                {
                    IsEligibleForOvertime = true,
                    IsEligibleForNightDifferential = true,
                },
            },
            DailyRecord = new DailyRecordRunModel
            {
                ShiftWorkingHour = 8,
                SpecialHolOTHours = otHours,
                SpecialHolNightDiffHours = ndHours,
                SpecialHolNightDiffOTHours = ndotHours,
            },
            Payload = new CalculatorPayload
            {
                CompanyPolicy = new CompanyPolicyRule { OtNdCalculationMethod = method },
            },
        };
    }

    private static void SetCompanyRate(PayrollContext context, RateType type, decimal rate)
        => context.Payload.PremiumRates[type] = rate;

    // --- Plain OT-only (SingleCategoryOTPolicy) --------------------------------------------

    [Fact]
    public void OtOnly_Compounded_MultipliesDayRateAndOtRate_UnaffectedByThisFeature()
    {
        var context = CreateContext(OtNdCalculationMethod.Compounded, otHours: 4);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m);

        var line = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(4 * HourlyRate * (1.30m * 1.25m)); // 438.75
        line.FlatOvertimeBase.Should().Be(4 * HourlyRate * 1.25m); // 337.50 -- unchanged Compounded-mode meaning
    }

    [Fact]
    public void OtOnly_Additive_DropsDayRateEntirely_UsesRawOtRateAlone()
    {
        var context = CreateContext(OtNdCalculationMethod.Additive, otHours: 4);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m);

        var line = new SpecialNonWorkingOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(4 * HourlyRate * 1.25m); // 337.50 -- day rate (1.30) dropped entirely
        line.Value.Should().BeLessThan(4 * HourlyRate * (1.30m * 1.25m));
        line.FlatOvertimeBase.Should().Be(4 * HourlyRate * 1.25m); // 337.50 -- same as Compounded's own FlatOvertimeBase
        line.OTPremium.Should().Be(4 * HourlyRate * 1.25m); // the entire amount is "premium" -- no day-rate portion to net out
    }

    // --- Plain ND-only (SingleCategoryNDPolicy) ---------------------------------------------

    [Fact]
    public void NdOnly_Compounded_MultipliesDayRateAndNdRate_UnaffectedByThisFeature()
    {
        var context = CreateContext(OtNdCalculationMethod.Compounded, ndHours: 5);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);

        var line = new SpecialNonWorkingNDPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(5 * HourlyRate * (1.30m * 1.10m)); // 482.625
        line.FlatNightDiffPremium.Should().Be(5 * HourlyRate * 0.10m); // already delta-only, unaffected by mode
    }

    [Fact]
    public void NdOnly_Additive_AddsDayRateAndNdPremiumDelta()
    {
        var context = CreateContext(OtNdCalculationMethod.Additive, ndHours: 5);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);

        var line = new SpecialNonWorkingNDPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        var expectedRate = 1.30m + (1.10m - 1.0m); // 1.40
        line.Value.Should().Be(5 * HourlyRate * expectedRate); // 472.50, less than Compounded's 482.625
        line.Value.Should().BeLessThan(5 * HourlyRate * (1.30m * 1.10m));
        // NDBasePay (Value - NDPremium) still isolates the pure day-rate portion in either mode.
        var ndBase = line.Value - line.NDPremium;
        ndBase.Should().Be(5 * HourlyRate * 1.30m);
    }

    // --- OT + ND combined (SingleCategoryNDOTPolicy) ----------------------------------------

    [Fact]
    public void Ndot_Compounded_MultipliesAllThreeRates_UnaffectedByThisFeature()
    {
        var context = CreateContext(OtNdCalculationMethod.Compounded, ndotHours: 3);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);

        var line = new SpecialNonWorkingNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        line.Value.Should().Be(3 * HourlyRate * (1.30m * 1.25m * 1.10m)); // 361.96875
    }

    [Fact]
    public void Ndot_Additive_DropsDayRate_UsesOtRatePlusNdDelta()
    {
        var context = CreateContext(OtNdCalculationMethod.Additive, ndotHours: 3);
        SetCompanyRate(context, RateType.SPECIAL_NON_WORKING, 1.30m);
        SetCompanyRate(context, RateType.HOLIDAY_OT, 1.25m);
        SetCompanyRate(context, RateType.NIGHTDIFF, 1.10m);

        var line = new SpecialNonWorkingNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), context);

        var expectedRate = 1.25m + (1.10m - 1.0m); // 1.35 -- day rate (1.30) dropped entirely
        line.Value.Should().Be(3 * HourlyRate * expectedRate); // 273.375, less than Compounded's 361.96875
        line.Value.Should().BeLessThan(3 * HourlyRate * (1.30m * 1.25m * 1.10m));
        line.FlatOvertimeBase.Should().Be(3 * HourlyRate * 1.25m); // 253.125 -- raw OT rate alone, same as Compounded's own FlatOvertimeBase
        line.FlatNightDiffPremium.Should().Be(3 * HourlyRate * (1.10m - 1.0m)); // 20.25 -- unaffected by mode
    }

    [Fact]
    public void RegularCategory_PlainOtOnly_DayRateOneMakesTwoWayCompoundingModeIndependent()
    {
        // dayRate x X == dayRate + (X - 1) whenever dayRate == 1, for exactly TWO factors --
        // Regular's plain OT-only hours (day x OT, nothing else) are identical in both modes.
        var compounded = CreateContext(OtNdCalculationMethod.Compounded);
        var additive = CreateContext(OtNdCalculationMethod.Additive);
        foreach (var ctx in new[] { compounded, additive })
        {
            SetCompanyRate(ctx, RateType.OVERTIME, 1.25m);
            ctx.DailyRecord.RegularOTHours = 3;
            ctx.DailyRecord.SpecialHolOTHours = 0;
        }

        var compoundedLine = new RegularOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), compounded);
        var additiveLine = new RegularOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), additive);

        compoundedLine.Value.Should().Be(additiveLine.Value);
    }

    [Fact]
    public void RegularCategory_Ndot_StillDiffersBetweenModes_EvenAtDayRateOne()
    {
        // The THREE-way case (day x OT x ND) has a genuine cross-term between OT and ND
        // themselves (OT x ND != OT + ND - 1 unless one of them is exactly 1), independent of
        // the day rate -- so even Regular category's NDOT hours are NOT mode-independent. This
        // is the exact mechanism behind the original "TOTAL (ITEMIZED) vs GROSS INCOME" gap
        // discussion earlier this session, which happened to occur entirely on the Regular
        // category (dayRate = 1) yet still showed a real, non-zero discrepancy.
        var compounded = CreateContext(OtNdCalculationMethod.Compounded);
        var additive = CreateContext(OtNdCalculationMethod.Additive);
        foreach (var ctx in new[] { compounded, additive })
        {
            SetCompanyRate(ctx, RateType.OVERTIME, 1.25m);
            SetCompanyRate(ctx, RateType.NIGHTDIFF, 1.10m);
            ctx.DailyRecord.RegularNDOTHours = 3;
            ctx.DailyRecord.SpecialHolNightDiffOTHours = 0;
        }

        var compoundedLine = new RegularNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), compounded);
        var additiveLine = new RegularNDOTPolicy().ApplyIfSatisfied(new BasicPipelineData(), additive);

        compoundedLine.Value.Should().Be(3 * HourlyRate * (1.25m * 1.10m)); // 278.4375
        additiveLine.Value.Should().Be(3 * HourlyRate * (1.25m + 1.10m - 1.0m)); // 273.375
        compoundedLine.Value.Should().NotBe(additiveLine.Value);
    }
}
