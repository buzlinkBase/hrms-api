using Hrms.Core.Calculators.DtrPay;
using Microsoft.Extensions.DependencyInjection;

namespace hrms.test.PayrollRunTests.DtrPay;

public class DtrPayBuildingBlockTests
{
    [Fact]
    public void Separate_SplitsNaturalDtrHoursIntoExclusiveBuckets()
    {
        // Natural DTR: Hrs 9 (3 at night), OT 3 (1 at night), ND 4 (3 regular + 1 OT), NDOT 1.
        var separated = CategoryHours.Separate(hrs: 9, ot: 3, nd: 4, ndot: 1);

        separated.Should().Be(new CategoryHours(Hrs: 6, Ot: 2, Nd: 3, Ndot: 1));
    }

    [Fact]
    public void NaturalHoursJoin_UndoesSeparate_WithNdotFoldedIn()
    {
        // Additive prices the natural hours: Hrs + ND, OT + NDOT, ND + NDOT, NDOT = 0.
        var natural = NaturalHours.Join(new CategoryHours(Hrs: 6, Ot: 2, Nd: 3, Ndot: 1));

        natural.Should().Be(new CategoryHours(Hrs: 9, Ot: 3, Nd: 4, Ndot: 0));
    }

    [Fact]
    public void Separate_NeverGoesNegative_OnInconsistentRows()
    {
        var separated = CategoryHours.Separate(hrs: 1, ot: 0, nd: 3, ndot: 1);

        separated.Should().Be(new CategoryHours(Hrs: 0, Ot: 0, Nd: 2, Ndot: 1));
    }

    [Fact]
    public void EligibleHoursFilter_NoShiftLength_PaysNoPremiumHours()
    {
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                Settings = new EmployeeSettingModel { IsEligibleForOvertime = true, IsEligibleForNightDifferential = true },
            },
            DailyRecord = new DailyRecordRunModel { ShiftWorkingHour = 0 },
        };

        var hours = new EligibleHoursFilter().Filter(new CategoryHours(8, 2, 3, 1), context);

        hours.Should().Be(new CategoryHours(8, 0, 0, 0));
    }

    [Theory]
    [InlineData(OtNdCalculationMethod.Compounded, typeof(CompoundedDtrPayCalculator))]
    [InlineData(OtNdCalculationMethod.Additive, typeof(AdditiveDtrPayCalculator))]
    public void Factory_PicksTheCalculatorForEachMethod(OtNdCalculationMethod method, Type expected)
    {
        DtrPayKit.Calculator(method).Should().BeOfType(expected);
    }

    [Fact]
    public void Factory_UnknownMethod_Throws()
    {
        var factory = new ServiceCollection().AddDtrPayCalculators().BuildServiceProvider()
            .GetRequiredService<IDtrPayCalculatorFactory>();

        var act = () => factory.For((OtNdCalculationMethod)99);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Default_IsAdditive_ForEveryFallback()
    {
        OtNdCalculationMethodDefaults.Default.Should().Be(OtNdCalculationMethod.Additive);
        new CompanyPolicyRule().OtNdCalculationMethod.Should().Be(OtNdCalculationMethod.Additive);
        // Already-generated runs keep their recorded method -- a row with none is a legacy Compounded run.
        new Payroll().OtNdCalculationMethod.Should().Be(OtNdCalculationMethod.Compounded);
    }
}
