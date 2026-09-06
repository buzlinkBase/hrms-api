using Hrms.Core.Services;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// Chains GetBasicPay -> ApplyOneTimeLeavePayoutsToGross -> GetGross -> GetNetPay in the same
/// order PayrollProcessorService.CalculateAsync's ProcessPayrollLine loop does, for a
/// Shared-funded (SSS Maternity) OneTime leave. Exists because the isolated single-method
/// tests (OneTimeLeavePayoutTests, PayrollProcessorUtilTests) missed exactly this kind of
/// interaction bug — GetGross used to re-include GovernmentFundedLeavePay into GrossIncome,
/// and CalculateAsync separately added it into NetPay again, double-counting it — see the
/// maternity-leave bug fix.
/// </summary>
public class OneTimeLeavePayoutPipelineTests
{
    private static readonly DateOnly PeriodStart = new(2026, 8, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 8, 15); // semi-monthly, 15 calendar days

    [Fact]
    public void FixedEmployee_SharedOneTimeMaternityLeave_SpanningWholePeriod_ComputesCorrectly()
    {
        var employee = new EmployeeModelPayrollRun
        {
            Id = Guid.NewGuid(),
            SalaryType = SalaryType.FIXED,
            MonthlyRate = 30_000,
            DailyRate = 1_000,
            PayrollGroup = new PayrollGroupModel { PayrollFrequency = PayrollFrequency.SEMI_MONTHLY },
        };
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(employee.Id)] = new List<LeaveApplication>
        {
            new()
            {
                // EmployerAdvancesPayment = true — the standard SSS Maternity case: the
                // employer advances the full benefit through payroll and files for
                // reimbursement afterward (see LeaveApplication.EmployerAdvancesPayment).
                Leave = new Leave { Description = "Maternity Leave", PaySource = PaySource.Shared, EmployerAdvancesPayment = true },
                PayoutMode = PayoutMode.OneTime,
                GovernmentAmount = 15_000,
                CompanyAmount = 5_000,
            },
        };
        payload.Leaves[new Leavekey(employee.Id)] = new List<LeaveApplication>
        {
            new()
            {
                Leave = new Leave { Description = "Maternity Leave", PaySource = PaySource.Shared },
                PayoutMode = PayoutMode.OneTime,
                PayType = PayType.WithPay,
                LeaveDateFrom = PeriodStart,
                LeaveDateTo = PeriodEnd,
            },
        };

        var line = new PayrollSummaryLine { PayPeriodStart = PeriodStart, PayPeriodEnd = PeriodEnd };

        // 1. Basic pay — every calendar day of the leave falls in this period (15 days), so
        // the flat rate is fully offset: 30,000/2 - 15*1,000 = 0. No DTR attendance in this
        // scenario (VirtualTimeComposer.IsEligibleForVirtualAttendance already excludes it).
        var oneTimeLeaveDays = PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, employee.Id, PeriodStart, PeriodEnd);
        PayrollProcessorService.GetBasicPay(line, new List<DTRPayModel>(), employee, oneTimeLeaveDays);
        line.BasicPay.Should().Be(0);
        line.GrossIncome += line.BasicPay; // mirrors ComputeBasicSalary's own accumulation

        // 2. OneTime payout — accumulates the funded amounts, does not touch GrossIncome.
        var employerAdvancedGovPay = PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, employee, line);
        line.GovernmentFundedLeavePay.Should().Be(15_000);
        line.CompanyFundedLeavePay.Should().Be(5_000);
        employerAdvancedGovPay.Should().Be(15_000); // Employer Advance — belongs in NetPay

        // 3. Gross rollup — only the Company-funded portion is included; Basic Pay
        // contributes 0 since the leave days were fully offset in step 1.
        line.GrossIncome = PayrollProcessorUtil.GetGross(line);
        line.GrossIncome.Should().Be(5_000);

        // 4. Deductions (stand-in for ComputeDeductions — a real WTax calculation isn't
        // exercised here, just its shape) computed off the correct (non-inflated) Gross, then
        // the employer-advanced government amount is added to NetPay exactly once, as
        // CalculateAsync does.
        var deductions = new DeductionPipeData { RunningTotal = 500 };
        line.NetPay = PayrollProcessorUtil.GetNetPay(line, deductions);
        line.NetPay += employerAdvancedGovPay;

        line.NetPay.Should().Be(19_500); // 5,000 - 500 + 15,000 — government amount counted once
    }

    [Fact]
    public void FixedEmployee_DirectDepositOneTimeMaternityLeave_ExcludedFromNetPay()
    {
        // Same scenario as the Employer Advance case above, except the government pays the
        // employee directly (e.g. the employee separated before the SSS claim was filed) — the
        // employer never hands this money over, so it must not inflate this run's NetPay, even
        // though it's still informationally tracked via GovernmentFundedLeavePay for payslip
        // visibility.
        var employee = new EmployeeModelPayrollRun
        {
            Id = Guid.NewGuid(),
            SalaryType = SalaryType.FIXED,
            MonthlyRate = 30_000,
            DailyRate = 1_000,
            PayrollGroup = new PayrollGroupModel { PayrollFrequency = PayrollFrequency.SEMI_MONTHLY },
        };
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(employee.Id)] = new List<LeaveApplication>
        {
            new()
            {
                Leave = new Leave { Description = "Maternity Leave", PaySource = PaySource.Shared, EmployerAdvancesPayment = true },
                PayoutMode = PayoutMode.OneTime,
                GovernmentAmount = 15_000,
                CompanyAmount = 5_000,
                EmployerAdvancesPayment = false, // per-application override: Direct Deposit
            },
        };
        payload.Leaves[new Leavekey(employee.Id)] = new List<LeaveApplication>
        {
            new()
            {
                Leave = new Leave { Description = "Maternity Leave", PaySource = PaySource.Shared },
                PayoutMode = PayoutMode.OneTime,
                PayType = PayType.WithPay,
                LeaveDateFrom = PeriodStart,
                LeaveDateTo = PeriodEnd,
            },
        };

        var line = new PayrollSummaryLine { PayPeriodStart = PeriodStart, PayPeriodEnd = PeriodEnd };

        var oneTimeLeaveDays = PayrollProcessorService.CountOneTimeLeaveCalendarDays(payload, employee.Id, PeriodStart, PeriodEnd);
        PayrollProcessorService.GetBasicPay(line, new List<DTRPayModel>(), employee, oneTimeLeaveDays);
        line.GrossIncome += line.BasicPay;

        var employerAdvancedGovPay = PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, employee, line);
        line.GovernmentFundedLeavePay.Should().Be(15_000); // still tracked informationally
        employerAdvancedGovPay.Should().Be(0);             // but excluded from NetPay

        line.GrossIncome = PayrollProcessorUtil.GetGross(line);
        line.GrossIncome.Should().Be(5_000); // unaffected by disbursement method either way

        var deductions = new DeductionPipeData { RunningTotal = 500 };
        line.NetPay = PayrollProcessorUtil.GetNetPay(line, deductions);
        line.NetPay += employerAdvancedGovPay;

        line.NetPay.Should().Be(4_500); // 5,000 - 500 — government amount excluded entirely
    }

    [Fact]
    public void VariableEmployee_SharedOneTimeMaternityLeave_BasicPayExcludesLeaveDays_ComputesCorrectly()
    {
        var employee = new EmployeeModelPayrollRun { Id = Guid.NewGuid(), SalaryType = SalaryType.VARIABLE };
        var payload = new CalculatorPayload();
        payload.OneTimeLeavePayouts[new EmployeeKey(employee.Id)] = new List<LeaveApplication>
        {
            new()
            {
                Leave = new Leave { Description = "Maternity Leave", PaySource = PaySource.Shared, EmployerAdvancesPayment = true },
                PayoutMode = PayoutMode.OneTime,
                GovernmentAmount = 15_000,
                CompanyAmount = 5_000,
            },
        };

        // Simulates the post-fix DTR output: the maternity-leave days contribute zero
        // RegularDayPay (IsEligibleForVirtualAttendance already excludes them from virtual
        // attendance), only the days actually worked before/after the leave do.
        var timeCalc = new List<DTRPayModel> { new() { RegularDayPay = 3_000 } };
        var line = new PayrollSummaryLine { PayPeriodStart = PeriodStart, PayPeriodEnd = PeriodEnd };

        PayrollProcessorService.GetBasicPay(line, timeCalc, employee, oneTimeLeaveDays: 0);
        line.BasicPay.Should().Be(3_000);
        line.GrossIncome += line.BasicPay;

        var employerAdvancedGovPay = PayrollProcessorService.ApplyOneTimeLeavePayoutsToGross(payload, employee, line);
        line.GrossIncome = PayrollProcessorUtil.GetGross(line);
        line.GrossIncome.Should().Be(8_000); // 3,000 worked + 5,000 company-funded

        var deductions = new DeductionPipeData { RunningTotal = 500 };
        line.NetPay = PayrollProcessorUtil.GetNetPay(line, deductions);
        line.NetPay += employerAdvancedGovPay;

        line.NetPay.Should().Be(22_500); // 8,000 - 500 + 15,000
    }
}
