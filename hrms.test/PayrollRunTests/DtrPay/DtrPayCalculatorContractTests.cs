using Hrms.Core.Calculators.DtrPay;

namespace hrms.test.PayrollRunTests.DtrPay;

/// <summary>
/// The contract every IDtrPayCalculator honours (Liskov): run against each pay method, so a new
/// method -- or a change to one -- can't quietly break what the payroll line service relies on.
/// </summary>
public abstract class DtrPayCalculatorContractTests
{
    protected abstract OtNdCalculationMethod Method { get; }

    private IDtrPayCalculator Calculator => DtrPayKit.Calculator(Method);

    private static PayrollContext Context(Action<DailyRecordRunModel> hours)
    {
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun
            {
                DailyRate = 800m,
                Settings = new EmployeeSettingModel
                {
                    IsEligibleForOvertime = true,
                    IsEligibleForNightDifferential = true,
                    IsEligibleForLeaveCredits = true,
                },
            },
            DailyRecord = new DailyRecordRunModel { ShiftWorkingHour = 8 },
            Payload = new CalculatorPayload(),
        };
        hours(context.DailyRecord);
        return context;
    }

    [Fact]
    public void ReportsItsOwnMethod() => Calculator.Method.Should().Be(Method);

    [Fact]
    public void NoHours_PayNothing()
    {
        var model = Calculator.Calculate(Context(_ => { }));

        model.TotalExcludingBasic.Should().Be(0m);
        model.RegularDayPay.Should().Be(0m);
        model.TotalOT.Should().Be(0m);
        model.TotalND.Should().Be(0m);
        model.TotalNDOT.Should().Be(0m);
    }

    [Fact]
    public void Gross_EqualsTheSumOfEveryCategoryBucket()
    {
        var model = Calculator.Calculate(Context(r =>
        {
            r.RegularNetHours = 6; r.RegularOTHours = 2; r.RegularNDHours = 2; r.RegularNDOTHours = 1;
            r.RestDayOTHours = 1; r.RestDayNDHours = 1; r.RestDayNDOTHours = 1;
            r.LegalHolOTHours = 1; r.LegalHolNightDiffHours = 1; r.LegalHolNightDiffOTHours = 1;
        }));

        var buckets =
            model.RegularDayPay + model.RegularOTPay + model.RegularNDPay + model.RegularNDOTPay
            + model.RestDayPay + model.RestDayOTPay + model.RestDayNDPay + model.RestDayNDOTPay
            + model.LegalPay + model.LegalOTPay + model.LegalNDPay + model.LegalNDOTPay
            + model.SpecialPay + model.SpecialOTPay + model.SpecialNDPay + model.SpecialNDOTPay
            + model.RestLegalPay + model.RestLegalOTPay + model.RestLegalNDPay + model.RestLegalNDOTPay
            + model.RestSpecialPay + model.RestSpecialOTPay + model.RestSpecialNDPay + model.RestSpecialNDOTPay
            + model.DoubleLegalPay + model.DoubleLegalOTPay + model.DoubleLegalNDPay + model.DoubleLegalNDOTPay
            + model.RestDoubleLegalPay + model.RestDoubleLegalOTPay + model.RestDoubleLegalNDPay + model.RestDoubleLegalNDOTPay;
        (model.RegularDayPay + model.TotalExcludingBasic).Should().BeApproximately(buckets, 0.0000001m);
        model.TotalExcludingBasic.Should().BeGreaterThan(0m);
    }

    [Fact]
    public void AttendanceAmounts_AreTheSameInEveryMethod()
    {
        var model = Calculator.Calculate(Context(r => { r.AbsentCount = 1; r.PaidLeaveHours = 4; }));

        model.AbsentAmount.Should().Be(800m);
        model.PaidLeave.Should().Be(100m * 4);
    }
}

public class CompoundedDtrPayCalculatorContractTests : DtrPayCalculatorContractTests
{
    protected override OtNdCalculationMethod Method => OtNdCalculationMethod.Compounded;
}

public class AdditiveDtrPayCalculatorContractTests : DtrPayCalculatorContractTests
{
    protected override OtNdCalculationMethod Method => OtNdCalculationMethod.Additive;
}
