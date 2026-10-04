using Hrms.Core.Calculators.DtrPay;
using Hrms.Core.Policies.DTRPolicies;
using hrms.test.PayrollRunTests.DtrPay;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Setup > Company Policy > OT/ND Calculation Method -- each method is its own IDtrPayCalculator
/// (Hrms.Core/Calculators/DtrPay):
/// The DTR stores natural hours (Hrs/OT include their night part, ND = all night hours, NDOT =
/// the night-OT subset of OT and ND).
/// - Compounded: separated hours at compounded rates (OT x day x OT, ND x day x ND, NDOT x day x OT x ND).
/// - Additive: the natural hours (NDOT = 0), one rate each -- day rate for Hrs (paid by the
///   day-type policy), raw OT rate for OT, (ND rate - 1) for ND. It pays exactly what the old
///   Additive branches paid (dayRate dropped for OT-involving hours, ND hours at day + (ND - 1)).
/// All on a Special Non-Working day: DailyRate 540 (hourly 67.50), SPECIAL_NON_WORKING 1.30,
/// HOLIDAY_OT 1.25, NIGHTDIFF 1.10.
/// </summary>
public class OtNdCalculationMethodTests
{
    private const decimal HourlyRate = 67.5m; // DailyRate 540 / 8
    private const decimal Day = 1.30m, Ot = 1.25m, Nd = 1.10m;

    // otHours / ndHours / ndotHours are the separated daytime-OT / night-regular / night-OT
    // buckets; the record stores them natural, as the DTR does (Hrs includes the night regular
    // hours, OT and ND each include the NDOT hours).
    private static PayrollContext CreateContext(
        double otHours = 0, double ndHours = 0, double ndotHours = 0,
        SalaryType salaryType = SalaryType.VARIABLE, bool otEligible = true, bool ndEligible = true)
    {
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                DailyRate = 540m,
                SalaryType = salaryType,
                Settings = new EmployeeSettingModel
                {
                    IsEligibleForOvertime = otEligible,
                    IsEligibleForNightDifferential = ndEligible,
                },
            },
            DailyRecord = new DailyRecordRunModel
            {
                ShiftWorkingHour = 8,
                SpecialHolHours = ndHours,
                SpecialHolOTHours = otHours + ndotHours,
                SpecialHolNightDiffHours = ndHours + ndotHours,
                SpecialHolNightDiffOTHours = ndotHours,
            },
            Payload = new CalculatorPayload(),
        };
        context.Payload.PremiumRates[RateType.SPECIAL_NON_WORKING] = Day;
        context.Payload.PremiumRates[RateType.HOLIDAY_OT] = Ot;
        context.Payload.PremiumRates[RateType.NIGHTDIFF] = Nd;
        context.Payload.PremiumRates[RateType.OVERTIME] = Ot;
        return context;
    }

    private static DTRPayModel Additive(PayrollContext context) =>
        DtrPayKit.Calculator(OtNdCalculationMethod.Additive).Calculate(context);

    private static DTRPayModel Compounded(PayrollContext context) =>
        DtrPayKit.Calculator(OtNdCalculationMethod.Compounded).Calculate(context);

    // --- Compounded: separated hours x compounded rates ----------------------------------------

    [Fact]
    public void Compounded_OtHours_MultiplyDayRateAndOtRate()
    {
        var pay = DtrPayKit.CompoundedOt(PayCategories.Special, CreateContext(otHours: 4));

        pay.Value.Should().Be(4 * HourlyRate * (Day * Ot)); // 438.75
        pay.FlatOvertimeBase.Should().Be(4 * HourlyRate * Ot); // 337.50
    }

    [Fact]
    public void Compounded_NdHours_MultiplyDayRateAndNdRate()
    {
        var pay = DtrPayKit.CompoundedNd(PayCategories.Special, CreateContext(ndHours: 5));

        pay.Value.Should().Be(5 * HourlyRate * (Day * Nd)); // 482.625
        pay.FlatNightDiffPremium.Should().Be(5 * HourlyRate * 0.10m);
    }

    [Fact]
    public void Compounded_NdotHours_MultiplyAllThreeRates()
    {
        var pay = DtrPayKit.CompoundedNdot(PayCategories.Special, CreateContext(ndotHours: 3));

        pay.Value.Should().Be(3 * HourlyRate * (Day * Ot * Nd)); // 361.96875
    }

    [Fact]
    public void Compounded_Calculator_ReportsEachSavedBucketOnItsOwn()
    {
        var context = CreateContext(otHours: 1, ndHours: 5, ndotHours: 3);

        var model = Compounded(context);

        model.SpecialOTPay.Should().Be(DtrPayKit.CompoundedOt(PayCategories.Special, context).Value);
        model.SpecialNDPay.Should().Be(DtrPayKit.CompoundedNd(PayCategories.Special, context).Value);
        model.SpecialNDOTPay.Should().Be(DtrPayKit.CompoundedNdot(PayCategories.Special, context).Value);
        model.SpecialPay.Should().Be(0m); // no plain holiday hours on this day
    }

    // --- Additive: natural hours x one rate each ---------------------------------------------

    [Fact]
    public void Additive_OtHours_PayTheRawOtRateAlone_NoDayRate()
    {
        var model = Additive(CreateContext(otHours: 4));

        model.SpecialOTPay.Should().Be(4 * HourlyRate * Ot); // 337.50 -- day rate (1.30) not applied
        model.SpecialOTBasePay.Should().Be(4 * HourlyRate * Ot);
        model.OTPremiumPay.Should().Be(4 * HourlyRate * Ot); // no day-rate portion to net out
    }

    [Fact]
    public void Additive_NdHours_BaseGoesToDayPay_NdBucketIsPremiumOnly()
    {
        var model = Additive(CreateContext(ndHours: 5));

        model.SpecialPay.Should().Be(5 * HourlyRate * Day); // natural Hrs, paid by the policy at the day rate
        model.SpecialNDPay.Should().Be(5 * HourlyRate * (Nd - 1.0m)); // the .10 only
        model.SpecialNDBasePay.Should().Be(0m);
        model.SpecialNDPremiumPay.Should().Be(5 * HourlyRate * (Nd - 1.0m));
        model.Holiday.Should().Be(5 * HourlyRate * Day);
        // Same money as the old Additive ND policy: 5 x hourly x (1.30 + 0.10) = 472.50.
        (model.SpecialPay + model.SpecialNDPay).Should().BeApproximately(5 * HourlyRate * (Day + Nd - 1.0m), 0.0000001m);
    }

    [Fact]
    public void Additive_NdotHours_FoldIntoOtAndNd_NdotIsAlwaysZero()
    {
        var model = Additive(CreateContext(ndotHours: 3));

        model.SpecialOTPay.Should().Be(3 * HourlyRate * Ot);
        model.SpecialNDPay.Should().Be(3 * HourlyRate * (Nd - 1.0m));
        model.SpecialNDOTPay.Should().Be(0m);
        model.SpecialNDOTBasePay.Should().Be(0m);
        model.SpecialNDOTPremiumPay.Should().Be(0m);
        model.TotalNDOT.Should().Be(0m);
        // Same money as the old Additive NDOT policy: 3 x hourly x (1.25 + 0.10) = 273.375.
        model.TotalExcludingBasic.Should().BeApproximately(3 * HourlyRate * (Ot + Nd - 1.0m), 0.0000001m);
    }

    [Fact]
    public void Additive_AllBuckets_SameGrossAndPremiumsAsBefore()
    {
        var model = Additive(CreateContext(otHours: 1, ndHours: 5, ndotHours: 3));

        var oldAdditiveGross =
            1 * HourlyRate * Ot                       // OT: raw OT rate
            + 5 * HourlyRate * (Day + Nd - 1.0m)      // ND: day + (ND - 1)
            + 3 * HourlyRate * (Ot + Nd - 1.0m);      // NDOT: OT + (ND - 1)
        model.TotalExcludingBasic.Should().BeApproximately(oldAdditiveGross, 0.0000001m);
        model.OTPremiumPay.Should().BeApproximately((1 + 3) * HourlyRate * Ot, 0.0000001m);
        model.NDPremiumPay.Should().BeApproximately((5 + 3) * HourlyRate * (Nd - 1.0m), 0.0000001m);
    }

    [Fact]
    public void Additive_PaysLessThanCompounded_WhenTheDayRateIsAboveOne()
    {
        var context = CreateContext(otHours: 1, ndHours: 5, ndotHours: 3);

        Additive(context).TotalExcludingBasic.Should().BeLessThan(Compounded(context).TotalExcludingBasic);
    }

    // --- Additive: the Fixed-salary Regular guard ---------------------------------------------

    [Theory]
    [InlineData(SalaryType.VARIABLE)]
    [InlineData(SalaryType.FIXED)]
    public void Additive_RegularNightHours_KeepTheirBaseWhereGrossCountsIt(SalaryType salaryType)
    {
        var context = CreateContext(salaryType: salaryType);
        context.DailyRecord.RegularNetHours = 5; // natural: all 5 regular hours are at night
        context.DailyRecord.RegularNDHours = 5;

        var model = Additive(context);

        var basePay = 5 * HourlyRate * 1.0m;
        var premium = 5 * HourlyRate * (Nd - 1.0m);
        if (salaryType == SalaryType.FIXED)
        {
            // Basic Pay is the monthly salary for FIXED, so the base stays in the ND bucket.
            model.RegularDayPay.Should().Be(0m);
            model.RegularNDPay.Should().Be(basePay + premium);
            model.RegularNDBasePay.Should().Be(basePay);
        }
        else
        {
            model.RegularDayPay.Should().Be(basePay);
            model.RegularNDPay.Should().Be(premium);
            model.RegularNDBasePay.Should().Be(0m);
        }
        model.RegularNDPremiumPay.Should().Be(premium);
    }

    // --- Eligibility applies the same way in both methods -------------------------------------

    [Theory]
    [InlineData(OtNdCalculationMethod.Compounded)]
    [InlineData(OtNdCalculationMethod.Additive)]
    public void NotNdEligible_NightAndNdotHoursPayNothing(OtNdCalculationMethod method)
    {
        var model = DtrPayKit.Calculator(method).Calculate(CreateContext(ndHours: 5, ndotHours: 3, ndEligible: false));

        model.TotalExcludingBasic.Should().Be(0m);
    }

    [Fact]
    public void Additive_NdotHours_OnlyNeedNdEligibility_SameAsBefore()
    {
        // NDOT hours were always gated on night-diff eligibility alone -- folding them into the
        // OT bucket must not suddenly require overtime eligibility too.
        var model = Additive(CreateContext(otHours: 2, ndotHours: 3, otEligible: false));

        model.SpecialOTPay.Should().Be(3 * HourlyRate * Ot); // NDOT hours only -- the 2 OT hours are ineligible
        model.SpecialNDPay.Should().Be(3 * HourlyRate * (Nd - 1.0m));
    }
}
