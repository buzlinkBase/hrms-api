using Hrms.Core.Calculators.DtrPay;
using Microsoft.Extensions.DependencyInjection;

namespace hrms.test.PayrollRunTests.DtrPay;

/// <summary>
/// Each pay method is one class per day type (Calculators/DtrPay/Additive and /Compounded, the
/// same split as Pipelines/DTRPayPipelines). Guards the wiring and the per-day-type exceptions.
/// </summary>
public class DayTypePayCalculatorTests
{
    private const decimal HourlyRate = 67.5m; // DailyRate 540 / 8

    private static readonly ServiceProvider Services = new ServiceCollection().AddDtrPayCalculators().BuildServiceProvider();

    private static PayrollContext Context(SalaryType salaryType, Action<DailyRecordRunModel> hours)
    {
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                DailyRate = 540m,
                SalaryType = salaryType,
                Settings = new EmployeeSettingModel { IsEligibleForOvertime = true, IsEligibleForNightDifferential = true },
            },
            DailyRecord = new DailyRecordRunModel { ShiftWorkingHour = 8 },
            Payload = new CalculatorPayload(),
        };
        context.Payload.PremiumRates[RateType.RESTDAY_DUTY] = 1.30m;
        context.Payload.PremiumRates[RateType.LEGAL_HOLIDAY_DUTY] = 2.00m;
        context.Payload.PremiumRates[RateType.HOLIDAY_OT] = 1.30m;
        context.Payload.PremiumRates[RateType.NIGHTDIFF] = 1.10m;
        hours(context.DailyRecord);
        return context;
    }

    [Fact]
    public void EachMethod_HasExactlyOneCalculatorPerDayType()
    {
        var everyKind = Enum.GetValues<PayCategoryKind>();

        Services.GetServices<IAdditiveDayTypePay>().Select(d => d.Kind).Should().BeEquivalentTo(everyKind);
        Services.GetServices<ICompoundedDayTypePay>().Select(d => d.Kind).Should().BeEquivalentTo(everyKind);
    }

    [Fact]
    public void Calculator_MissingADayType_Throws()
    {
        var sevenOfEight = Services.GetServices<IAdditiveDayTypePay>().Where(d => d.Kind != PayCategoryKind.RestDay);

        var act = () => new AdditiveDtrPayCalculator(
            sevenOfEight,
            Services.GetRequiredService<IAttendancePayCalculator>(),
            Services.GetRequiredService<IDtrPayModelBuilder>());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Calculator_DuplicateDayType_Throws()
    {
        var all = Services.GetServices<ICompoundedDayTypePay>().ToList();

        var act = () => new CompoundedDtrPayCalculator(
            all.Append(all[0]),
            Services.GetRequiredService<IAttendancePayCalculator>(),
            Services.GetRequiredService<IDtrPayModelBuilder>());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AdditiveRestDay_NightBaseAlwaysGoesToDayPay_EvenForFixedSalary()
    {
        // Only the Regular day type keeps a Fixed employee's night base in the ND bucket --
        // Rest Day pay is counted in gross for Fixed employees too, so it folds normally.
        var restDay = Services.GetServices<IAdditiveDayTypePay>().Single(d => d.Kind == PayCategoryKind.RestDay);

        // Natural hours: both rest-day hours were at night.
        var pay = restDay.Calculate(Context(SalaryType.FIXED, r => { r.RestDayHours = 2; r.RestDayNDHours = 2; }));

        pay.Day.Value.Should().Be(2 * HourlyRate * 1.30m);
        pay.Nd.Value.Should().Be(2 * HourlyRate * 0.10m);
        pay.Nd.BasePortion.Should().Be(0m);
    }

    [Fact]
    public void LegalHoliday_SameHours_AdditiveVsCompounded()
    {
        // Natural hours: 3 OT hours, 1 of them at night (so ND and NDOT are both 1).
        var context = Context(SalaryType.VARIABLE, r => { r.LegalHolOTHours = 3; r.LegalHolNightDiffHours = 1; r.LegalHolNightDiffOTHours = 1; });
        var additive = Services.GetServices<IAdditiveDayTypePay>().Single(d => d.Kind == PayCategoryKind.Legal).Calculate(context);
        var compounded = Services.GetServices<ICompoundedDayTypePay>().Single(d => d.Kind == PayCategoryKind.Legal).Calculate(context);

        // Additive: OT + NDOT at the raw OT rate, NDOT's night premium in ND, NDOT itself 0.
        additive.Ot.Value.Should().Be(3 * HourlyRate * 1.30m);
        additive.Nd.Value.Should().Be(1 * HourlyRate * 0.10m);
        additive.Ndot.Should().Be(BucketPay.Zero);

        // Compounded: each separated bucket on its own, at day x OT (x ND).
        compounded.Ot.Value.Should().Be(2 * HourlyRate * (2.00m * 1.30m));
        compounded.Ndot.Value.Should().Be(1 * HourlyRate * (2.00m * 1.30m * 1.10m));
        compounded.Nd.Value.Should().Be(0m);
    }
}
