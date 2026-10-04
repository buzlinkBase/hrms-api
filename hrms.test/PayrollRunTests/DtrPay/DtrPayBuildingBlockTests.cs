using Hrms.Core.Calculators.DtrPay;
using Microsoft.Extensions.DependencyInjection;

namespace hrms.test.PayrollRunTests.DtrPay;

public class DtrPayBuildingBlockTests
{
    [Fact]
    public void DisplayHoursShaper_MatchesTheDtrScreensFold()
    {
        // Same fold as the UI's toDisplayHours: Hrs + ND, OT + NDOT, ND + NDOT, NDOT = 0.
        var shown = DisplayHoursShaper.Shape(new CategoryHours(Hrs: 6, Ot: 2, Nd: 3, Ndot: 1));

        shown.Should().Be(new CategoryHours(Hrs: 9, Ot: 3, Nd: 4, Ndot: 0));
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
