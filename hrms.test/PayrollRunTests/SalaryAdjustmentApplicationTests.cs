using Hrms.Core.Services;
using Hrms.Domain.Entities;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// EmployeePayrollLineService.ApplySalaryAdjustments — reused as-is by both the regular payroll
/// flow and LastPayrollService's HR-confirmed Salary Adjustments (see Last Pay consolidation).
/// First direct test coverage for this method; it previously had none despite being existing,
/// shipped logic. Confirms the existing (deliberate) quirk: Salary/Allowance amounts are added
/// straight to Gross/Net without being re-run through WTax, since this runs after
/// ComputeDeductions in the regular flow.
/// </summary>
public class SalaryAdjustmentApplicationTests
{
    private static EmployeeModelPayrollRun Employee() => new() { Id = Guid.NewGuid(), FullName = "Test Employee" };

    private static PayrollSummaryLine Line() => new()
    {
        BasicPay = 10_000,
        GrossIncome = 10_000,
        NetPay = 9_000,
        TotalOtherIncome = 0,
        OtherDeductions = 0,
        TotalDeductions = 1_000,
    };

    private static SalaryAdjustment Adjustment(SalaryAdjustmentType type, decimal amount) => new()
    {
        AdjustmentType = type,
        Amount = amount,
    };

    [Fact]
    public void NoAdjustmentsForEmployee_LeavesLineUntouched()
    {
        var employee = Employee();
        var payload = new CalculatorPayload();
        var line = Line();

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.GrossIncome.Should().Be(10_000);
        line.NetPay.Should().Be(9_000);
    }

    [Fact]
    public void SalaryAdjustment_AddsToBasicPayGrossAndNetPay_Untaxed()
    {
        var employee = Employee();
        var payload = new CalculatorPayload();
        payload.SalaryAdjustments[new EmployeeKey(employee.Id)] = new List<SalaryAdjustment>
        {
            Adjustment(SalaryAdjustmentType.Salary, 5_000),
        };
        var line = Line();

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.BasicPay.Should().Be(15_000);
        line.GrossIncome.Should().Be(15_000);
        line.NetPay.Should().Be(14_000); // full amount reaches NetPay — no WTax re-applied
    }

    [Fact]
    public void AllowanceAdjustment_AddsToOtherIncomeGrossAndNetPay_Untaxed()
    {
        var employee = Employee();
        var payload = new CalculatorPayload();
        payload.SalaryAdjustments[new EmployeeKey(employee.Id)] = new List<SalaryAdjustment>
        {
            Adjustment(SalaryAdjustmentType.Allowance, 2_000),
        };
        var line = Line();

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.TotalOtherIncome.Should().Be(2_000);
        line.GrossIncome.Should().Be(12_000);
        line.NetPay.Should().Be(11_000);
    }

    [Fact]
    public void DeductionAdjustment_SubtractsFromNetPay_AddsToTotalDeductions()
    {
        var employee = Employee();
        var payload = new CalculatorPayload();
        payload.SalaryAdjustments[new EmployeeKey(employee.Id)] = new List<SalaryAdjustment>
        {
            Adjustment(SalaryAdjustmentType.Deduction, 1_500),
        };
        var line = Line();

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.OtherDeductions.Should().Be(1_500);
        line.TotalDeductions.Should().Be(2_500);
        line.NetPay.Should().Be(7_500);
        line.GrossIncome.Should().Be(10_000); // deductions never touch Gross
    }

    [Fact]
    public void MultipleAdjustmentsOfDifferentTypes_AllApplyAdditively()
    {
        var employee = Employee();
        var payload = new CalculatorPayload();
        payload.SalaryAdjustments[new EmployeeKey(employee.Id)] = new List<SalaryAdjustment>
        {
            Adjustment(SalaryAdjustmentType.Salary, 5_000),
            Adjustment(SalaryAdjustmentType.Allowance, 2_000),
            Adjustment(SalaryAdjustmentType.Deduction, 1_000),
        };
        var line = Line();

        EmployeePayrollLineService.ApplySalaryAdjustments(payload, employee, line);

        line.GrossIncome.Should().Be(17_000); // 10,000 + 5,000 + 2,000
        line.NetPay.Should().Be(15_000);      // 9,000 + 5,000 + 2,000 - 1,000
    }
}

/// <summary>
/// LastPayrollService.ApplyConfirmedOtherIncome — the Last Pay equivalent for HR-confirmed
/// Other Income (allowance) schedule rows, mirroring ApplySalaryAdjustments' Allowance-branch
/// treatment (untaxed direct addition) but keyed per-row off IsTaxable rather than a single
/// AdjustmentType.
/// </summary>
public class ApplyConfirmedOtherIncomeTests
{
    private static PayrollSummaryLine Line() => new()
    {
        GrossIncome = 10_000,
        NetPay = 9_000,
        TotalOtherIncome = 0,
        TaxableBenefits = 0,
        NonTaxableBenefits = 0,
    };

    [Fact]
    public void TaxableSchedule_AddsToGrossNetAndTaxableBenefits()
    {
        var line = Line();
        var schedules = new List<OtherIncomeSchedules>
        {
            new() { Amount = 3_000, IsTaxable = true },
        };

        LastPayrollService.ApplyConfirmedOtherIncome(schedules, line);

        line.GrossIncome.Should().Be(13_000);
        line.NetPay.Should().Be(12_000);
        line.TotalOtherIncome.Should().Be(3_000);
        line.TaxableBenefits.Should().Be(3_000);
        line.NonTaxableBenefits.Should().Be(0);
    }

    [Fact]
    public void NonTaxableSchedule_AddsToNonTaxableBenefitsInstead()
    {
        var line = Line();
        var schedules = new List<OtherIncomeSchedules>
        {
            new() { Amount = 1_500, IsTaxable = false },
        };

        LastPayrollService.ApplyConfirmedOtherIncome(schedules, line);

        line.NonTaxableBenefits.Should().Be(1_500);
        line.TaxableBenefits.Should().Be(0);
        line.GrossIncome.Should().Be(11_500);
        line.NetPay.Should().Be(10_500);
    }

    [Fact]
    public void MultipleSchedules_ApplyAdditively()
    {
        var line = Line();
        var schedules = new List<OtherIncomeSchedules>
        {
            new() { Amount = 1_000, IsTaxable = true },
            new() { Amount = 2_000, IsTaxable = false },
        };

        LastPayrollService.ApplyConfirmedOtherIncome(schedules, line);

        line.GrossIncome.Should().Be(13_000);
        line.NetPay.Should().Be(12_000);
        line.TaxableBenefits.Should().Be(1_000);
        line.NonTaxableBenefits.Should().Be(2_000);
    }

    [Fact]
    public void EmptyList_LeavesLineUntouched()
    {
        var line = Line();

        LastPayrollService.ApplyConfirmedOtherIncome(new List<OtherIncomeSchedules>(), line);

        line.GrossIncome.Should().Be(10_000);
        line.NetPay.Should().Be(9_000);
    }
}
