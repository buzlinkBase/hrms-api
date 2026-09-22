using DTR.Core;
using Hrms.Core.Policies.DeductionPolicies;
using Hrms.Domain.Entities;

namespace Hrms.Core.Services;

// Builds one employee's PayrollSummaryLine for one regular-payroll period — extracted from
// PayrollProcessorService (formerly its CalculateAsync loop body + ~15 private/internal
// helpers) so that class could shrink to a thin orchestration Facade. Pure Extract Class: no
// calculation logic changed here, only relocated.
public class EmployeePayrollLineService
{
    private readonly IDailyRateResolver _dailyRateResolver;
    private readonly ICalculator<DTRPayModel, PayrollContext> _basicPayrollCalculator;
    private readonly ICalculator<AllowancePipeData, PayrollContext> _allowancesCalculator;
    private readonly ICalculator<DeductionPipeData, DeductionPayloadContext> _deductionCalculator;

    public EmployeePayrollLineService(
        IDailyRateResolver dailyRateResolver,
        ICalculator<DTRPayModel, PayrollContext> basicPayrollCalculator,
        ICalculator<AllowancePipeData, PayrollContext> allowancesCalculator,
        ICalculator<DeductionPipeData, DeductionPayloadContext> deductionCalculator)
    {
        _dailyRateResolver = dailyRateResolver;
        _basicPayrollCalculator = basicPayrollCalculator;
        _allowancesCalculator = allowancesCalculator;
        _deductionCalculator = deductionCalculator;
    }

    public PayrollSummaryLine Calculate(
        DateRangePayload dateRange,
        List<DailyRecordRunModel> empDtr,
        EmployeeModelPayrollRun employee,
        CalculatorPayload rangePayload,
        Guid batchId,
        string period,
        CrossMonthStatutoryCreditPolicy creditPolicy,
        CrossMonthStatutoryCreditPolicy wtaxCreditPolicy,
        DateOnly? payDate,
        string? remarks,
        List<LeaveMetaDataModel>? empLeaveInfo,
        Dictionary<Guid, PaySource> leavePaySourceMap)
    {
        employee.DailyRate = _dailyRateResolver.Resolve(employee, dateRange.FromDate);
        var payrollLine = InitializePayrollLine(
            dateRange, employee, batchId, period, creditPolicy, wtaxCreditPolicy, payDate, remarks,
            rangePayload.CompanyPolicy.OtNdCalculationMethod);

        ComputeHoursBreakdown(empDtr, payrollLine);
        ComputeRetirementAccrual(employee, rangePayload, payrollLine);
        payrollLine.PaidLeaveBreakdown = BuildPaidLeaveBreakdown(empLeaveInfo);
        ComputeBasicSalary(dateRange, empDtr, employee, rangePayload, payrollLine, leavePaySourceMap);
        rangePayload.Leaves.TryGetValue(new Leavekey(employee.Id), out var employeeLeaveApps);
        var employeeOneTimeLeaveApps = employeeLeaveApps?.Where(a => a.PayoutMode == PayoutMode.OneTime).ToList();
        ComputeNonCompanyPaidLeaves(empLeaveInfo, leavePaySourceMap, employeeOneTimeLeaveApps, payrollLine);
        ComputeAllowances(dateRange, rangePayload, employee, payrollLine);
        // Deliberately before ComputeDeductions (unlike ApplySalaryAdjustments, which runs
        // after) — the Company-funded portion must already be part of GrossIncome so the
        // SSS/PHIC/HDMF/WTax calculators below see it via StatutoryHelper.Get*GrossBaseRate.
        var employerAdvancedGovPay = ApplyOneTimeLeavePayoutsToGross(rangePayload, employee, payrollLine);
        payrollLine.GrossIncome = PayrollProcessorUtil.GetGross(payrollLine);
        ComputeDeductions(rangePayload, employee, payrollLine);
        // ComputeDeductions just freshly recomputed NetPay from GrossIncome (not an
        // increment), so the employer-advanced government portion — deliberately kept out
        // of GrossIncome/statutory bases above — can only be added to NetPay here, after.
        // Direct-deposit payouts (government pays the employee, not this employer) are
        // excluded — see ApplyOneTimeLeavePayoutsToGross's doc comment.
        payrollLine.NetPay += employerAdvancedGovPay;
        ApplySalaryAdjustments(rangePayload, employee, payrollLine);
        return payrollLine;
    }

    private static string BuildPayrollPeriod(DateRangePayload payload)
    {
        return string.Concat(
               payload.FromDate.ToString("MMM-dd-yyyy"), " ",
               payload.ToDate.ToString("MMM-dd-yyyy"),
               string.Empty);
    }

    private static PayrollSummaryLine InitializePayrollLine(
        DateRangePayload payload,
        EmployeeModelPayrollRun employee,
        Guid payrollBatchId,
        string period,
        CrossMonthStatutoryCreditPolicy creditPolicy,
        CrossMonthStatutoryCreditPolicy wtaxCreditPolicy,
        DateOnly? payDate,
        string? remarks,
        OtNdCalculationMethod otNdCalculationMethod) =>
        new PayrollSummaryLine
        {
            PayrollPeriod = period,
            PayPeriodStart = payload.FromDate,
            PayPeriodEnd = payload.ToDate,
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            PayrollBatchId = payrollBatchId,
            Remarks = remarks,
            PayrollDate = payload.ToDate,
            StatutoryCreditDate = StatutoryCreditDateResolver.Resolve(payload.FromDate, payload.ToDate, creditPolicy, payDate),
            PostingPeriod = StatutoryCreditDateResolver.Resolve(payload.FromDate, payload.ToDate, wtaxCreditPolicy, payDate),
            PayDate = payDate,
            PayrollGroupId = employee.PayrollGroupId,
            AreaId = employee.AreaId,
            ClientId = employee.ClientId,
            // Recorded per-run (not recomputed live) so a payslip printed later reproduces
            // exactly what was actually paid, even if the company-wide setting changes
            // afterward — see PayslipHoursDocument.BuildRows, which needs to know which mode
            // produced this row's {Category}OTBasePay/NDOTBasePay figures to present them
            // correctly.
            OtNdCalculationMethod = otNdCalculationMethod,
        };

    // Straight per-employee sum of DailyRecord's own per-category hour fields across every
    // day in the period — the DTR posting pipeline already computed these (see
    // DailyRecordRunModel/DailyRecord), this just carries them forward instead of letting
    // them get dropped once the DTR rows are rolled up into pay amounts. No payroll math of
    // its own; OvertimeHours is the one derived figure — the sum of every pure *OTHours
    // category (excluding the ND-OT combo hours, which are their own columns) — matching
    // what the Payroll Summary "OT Total Hr" figure means.
    internal static void ComputeHoursBreakdown(List<DailyRecordRunModel> dtrs, PayrollSummaryLine line)
    {
        decimal Sum(Func<DailyRecordRunModel, double> selector) => (decimal)dtrs.Sum(selector);

        line.RegularNetHours = Sum(x => x.RegularNetHours);
        line.RegularOTHours = Sum(x => x.RegularOTHours);
        line.RegularNDHours = Sum(x => x.RegularNDHours);
        line.RegularNDOTHours = Sum(x => x.RegularNDOTHours);

        line.RestDayHours = Sum(x => x.RestDayHours);
        line.RestDayOTHours = Sum(x => x.RestDayOTHours);
        line.RestDayNDHours = Sum(x => x.RestDayNDHours);
        line.RestDayNDOTHours = Sum(x => x.RestDayNDOTHours);

        line.LegalHolHours = Sum(x => x.LegalHolHours);
        line.LegalHolOTHours = Sum(x => x.LegalHolOTHours);
        line.LegalHolNightDiffHours = Sum(x => x.LegalHolNightDiffHours);
        line.LegalHolNightDiffOTHours = Sum(x => x.LegalHolNightDiffOTHours);

        line.SpecialHolHours = Sum(x => x.SpecialHolHours);
        line.SpecialHolOTHours = Sum(x => x.SpecialHolOTHours);
        line.SpecialHolNightDiffHours = Sum(x => x.SpecialHolNightDiffHours);
        line.SpecialHolNightDiffOTHours = Sum(x => x.SpecialHolNightDiffOTHours);

        line.RestLegalDayHours = Sum(x => x.RestLegalDayHours);
        line.RestLegalDayOTHours = Sum(x => x.RestLegalDayOTHours);
        line.RestLegalDayNDHours = Sum(x => x.RestLegalDayNDHours);
        line.RestLegalDayNDOTHours = Sum(x => x.RestLegalDayNDOTHours);

        line.RestSpecialDayHours = Sum(x => x.RestSpecialDayHours);
        line.RestSpecialDayOTHours = Sum(x => x.RestSpecialDayOTHours);
        line.RestSpecialDayNDHours = Sum(x => x.RestSpecialDayNDHours);
        line.RestSpecialDayNDOTHours = Sum(x => x.RestSpecialDayNDOTHours);

        line.DoubleLegalHours = Sum(x => x.DoubleLegalHours);
        line.DoubleLegalOTHours = Sum(x => x.DoubleLegalOTHours);
        line.DoubleLegalNDHours = Sum(x => x.DoubleLegalNDHours);
        line.DoubleLegalNDOTHours = Sum(x => x.DoubleLegalNDOTHours);

        line.RestDoubleLegalHours = Sum(x => x.RestDoubleLegalHours);
        line.RestDoubleLegalOTHours = Sum(x => x.RestDoubleLegalOTHours);
        line.RestDoubleLegalNDHours = Sum(x => x.RestDoubleLegalNDHours);
        line.RestDoubleLegalNDOTHours = Sum(x => x.RestDoubleLegalNDOTHours);

        line.OBHours = (decimal)dtrs.Sum(x => x.OBHours);
        line.PaidLeaveHours = (decimal)dtrs.Sum(x => x.PaidLeaveHours);
        line.UnpaidLeaveHours = (decimal)dtrs.Sum(x => x.UnpaidLeaveHours);

        line.OvertimeHours = line.RegularOTHours + line.RestDayOTHours +
            line.LegalHolOTHours + line.SpecialHolOTHours +
            line.RestLegalDayOTHours + line.RestSpecialDayOTHours +
            line.DoubleLegalOTHours + line.RestDoubleLegalOTHours;

    }

    // Setup > Client > Settings > Allowances > Retirement (days/year). Runs every payroll per
    // the client's own formula; purely informational on this run -- deliberately never folds
    // into GrossIncome/NetPay (unlike ComputeAllowances/ApplySalaryAdjustments). The actual
    // RetirementFund.Balance only grows by this amount at Post time (see
    // PayrollService.ProcessRetirementFundActivityAsync), mirroring why DeductionApplicationDetail.
    // Balance is deferred to Post: a regenerated or deleted draft must never corrupt a running
    // balance.
    internal static void ComputeRetirementAccrual(
        EmployeeModelPayrollRun employee, CalculatorPayload rangePayload, PayrollSummaryLine payrollLine)
    {
        if (employee.ClientId is not { } clientId) return;
        if (!rangePayload.ClientRetirementDaysPerYear.TryGetValue(clientId, out var daysPerYear) || daysPerYear is not { } days)
            return; // no key, or a null value, means this client gives no retirement benefit

        var hourlyRate = employee.DailyRate / 8m;
        payrollLine.RetirementAccrual = hourlyRate * (payrollLine.RegularNetHours * days / 12m / 30m);
    }

    // Which leave type(s) made up this run's PaidLeaves/UnpaidLeaves totals, and how many
    // hours each — grouped by leave + pay type since the same leave can appear as both Paid
    // and Unpaid across different days in one run (e.g. balance ran out mid-period). Purely
    // a display aid; does not change PaidLeaves/UnpaidLeaves/GrossIncome, which are computed
    // elsewhere from the same DTR days.
    internal static string? BuildPaidLeaveBreakdown(List<LeaveMetaDataModel>? leaveInfo)
    {
        if (leaveInfo == null || leaveInfo.Count == 0) return null;
        var parts = leaveInfo
            .GroupBy(x => new { x.Name, x.PayType })
            .Select(g => $"{g.Key.Name}: {g.Sum(x => x.Hours):0.##}h ({(g.Key.PayType == PayType.WithoutPay ? "Unpaid" : "Paid")})")
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private void ComputeBasicSalary(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload rangePayload,
        PayrollSummaryLine payrollLine,
        Dictionary<Guid, PaySource> leavePaySourceMap)
    {
        var employeeBasicCalc = CalculateDTRTimePay(payload, dtrs, employee, rangePayload);
        payrollLine.TimeHourPayResults = employeeBasicCalc;
        payrollLine.SalaryType = employee.SalaryType;
        payrollLine.DailyRate = employee.DailyRate;
        var oneTimeLeaveDays = CountOneTimeLeaveCalendarDays(rangePayload, employee.Id, payload.FromDate, payload.ToDate);
        GetBasicPay(payrollLine, employeeBasicCalc, employee, oneTimeLeaveDays);
        payrollLine.GrossIncome += payrollLine.BasicPay + employeeBasicCalc.Sum(x => x.TotalExcludingBasic);
        payrollLine.LateAmount = employeeBasicCalc.Sum(x => x.LateAmount);
        payrollLine.OvertimePay = employeeBasicCalc.Sum(x => x.TotalOT);
        payrollLine.UnderTimeAmount = employeeBasicCalc.Sum(x => x.UTAmount);
        payrollLine.NightDifferentialPay = employeeBasicCalc.Sum(x => x.TotalND);
        payrollLine.NightDifferentialOTPay = employeeBasicCalc.Sum(x => x.TotalNDOT);
        payrollLine.OTPremiumPay = employeeBasicCalc.Sum(x => x.OTPremiumPay);
        payrollLine.NDPremiumPay = employeeBasicCalc.Sum(x => x.NDPremiumPay);
        payrollLine.AbsencesAmount = employeeBasicCalc.Sum(x => x.AbsentAmount);

        payrollLine.RegularDayPay = employeeBasicCalc.Sum(x => x.RegularDayPay);
        payrollLine.RegularOTPay = employeeBasicCalc.Sum(x => x.RegularOTPay);
        payrollLine.RegularNDPay = employeeBasicCalc.Sum(x => x.RegularNDPay);
        payrollLine.RegularNDOTPay = employeeBasicCalc.Sum(x => x.RegularNDOTPay);
        payrollLine.RegularNDBasePay = employeeBasicCalc.Sum(x => x.RegularNDBasePay);
        payrollLine.RegularNDPremiumPay = employeeBasicCalc.Sum(x => x.RegularNDPremiumPay);
        payrollLine.RegularNDOTBasePay = employeeBasicCalc.Sum(x => x.RegularNDOTBasePay);
        payrollLine.RegularOTBasePay = employeeBasicCalc.Sum(x => x.RegularOTBasePay);
        payrollLine.RegularNDOTPremiumPay = employeeBasicCalc.Sum(x => x.RegularNDOTPremiumPay);

        payrollLine.RestDayPay = employeeBasicCalc.Sum(x => x.RestDayPay);
        payrollLine.RestDayOTPay = employeeBasicCalc.Sum(x => x.RestDayOTPay);
        payrollLine.RestDayNDPay = employeeBasicCalc.Sum(x => x.RestDayNDPay);
        payrollLine.RestDayNDOTPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPay);
        payrollLine.RestDayNDBasePay = employeeBasicCalc.Sum(x => x.RestDayNDBasePay);
        payrollLine.RestDayNDPremiumPay = employeeBasicCalc.Sum(x => x.RestDayNDPremiumPay);
        payrollLine.RestDayNDOTBasePay = employeeBasicCalc.Sum(x => x.RestDayNDOTBasePay);
        payrollLine.RestDayOTBasePay = employeeBasicCalc.Sum(x => x.RestDayOTBasePay);
        payrollLine.RestDayNDOTPremiumPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPremiumPay);

        payrollLine.LegalPay = employeeBasicCalc.Sum(x => x.LegalPay);
        payrollLine.LegalOTPay = employeeBasicCalc.Sum(x => x.LegalOTPay);
        payrollLine.LegalNDPay = employeeBasicCalc.Sum(x => x.LegalNDPay);
        payrollLine.LegalNDOTPay = employeeBasicCalc.Sum(x => x.LegalNDOTPay);
        payrollLine.LegalNDBasePay = employeeBasicCalc.Sum(x => x.LegalNDBasePay);
        payrollLine.LegalNDPremiumPay = employeeBasicCalc.Sum(x => x.LegalNDPremiumPay);
        payrollLine.LegalNDOTBasePay = employeeBasicCalc.Sum(x => x.LegalNDOTBasePay);
        payrollLine.LegalOTBasePay = employeeBasicCalc.Sum(x => x.LegalOTBasePay);
        payrollLine.LegalNDOTPremiumPay = employeeBasicCalc.Sum(x => x.LegalNDOTPremiumPay);

        payrollLine.SpecialPay = employeeBasicCalc.Sum(x => x.SpecialPay);
        payrollLine.SpecialOTPay = employeeBasicCalc.Sum(x => x.SpecialOTPay);
        payrollLine.SpecialNDPay = employeeBasicCalc.Sum(x => x.SpecialNDPay);
        payrollLine.SpecialNDOTPay = employeeBasicCalc.Sum(x => x.SpecialNDOTPay);
        payrollLine.SpecialNDBasePay = employeeBasicCalc.Sum(x => x.SpecialNDBasePay);
        payrollLine.SpecialNDPremiumPay = employeeBasicCalc.Sum(x => x.SpecialNDPremiumPay);
        payrollLine.SpecialNDOTBasePay = employeeBasicCalc.Sum(x => x.SpecialNDOTBasePay);
        payrollLine.SpecialOTBasePay = employeeBasicCalc.Sum(x => x.SpecialOTBasePay);
        payrollLine.SpecialNDOTPremiumPay = employeeBasicCalc.Sum(x => x.SpecialNDOTPremiumPay);

        payrollLine.RestLegalPay = employeeBasicCalc.Sum(x => x.RestLegalPay);
        payrollLine.RestLegalOTPay = employeeBasicCalc.Sum(x => x.RestLegalOTPay);
        payrollLine.RestLegalNDPay = employeeBasicCalc.Sum(x => x.RestLegalNDPay);
        payrollLine.RestLegalNDOTPay = employeeBasicCalc.Sum(x => x.RestLegalNDOTPay);
        payrollLine.RestLegalNDBasePay = employeeBasicCalc.Sum(x => x.RestLegalNDBasePay);
        payrollLine.RestLegalNDPremiumPay = employeeBasicCalc.Sum(x => x.RestLegalNDPremiumPay);
        payrollLine.RestLegalNDOTBasePay = employeeBasicCalc.Sum(x => x.RestLegalNDOTBasePay);
        payrollLine.RestLegalOTBasePay = employeeBasicCalc.Sum(x => x.RestLegalOTBasePay);
        payrollLine.RestLegalNDOTPremiumPay = employeeBasicCalc.Sum(x => x.RestLegalNDOTPremiumPay);

        payrollLine.RestSpecialPay = employeeBasicCalc.Sum(x => x.RestSpecialPay);
        payrollLine.RestSpecialOTPay = employeeBasicCalc.Sum(x => x.RestSpecialOTPay);
        payrollLine.RestSpecialNDPay = employeeBasicCalc.Sum(x => x.RestSpecialNDPay);
        payrollLine.RestSpecialNDOTPay = employeeBasicCalc.Sum(x => x.RestSpecialNDOTPay);
        payrollLine.RestSpecialNDBasePay = employeeBasicCalc.Sum(x => x.RestSpecialNDBasePay);
        payrollLine.RestSpecialNDPremiumPay = employeeBasicCalc.Sum(x => x.RestSpecialNDPremiumPay);
        payrollLine.RestSpecialNDOTBasePay = employeeBasicCalc.Sum(x => x.RestSpecialNDOTBasePay);
        payrollLine.RestSpecialOTBasePay = employeeBasicCalc.Sum(x => x.RestSpecialOTBasePay);
        payrollLine.RestSpecialNDOTPremiumPay = employeeBasicCalc.Sum(x => x.RestSpecialNDOTPremiumPay);

        payrollLine.DoubleLegalPay = employeeBasicCalc.Sum(x => x.DoubleLegalPay);
        payrollLine.DoubleLegalOTPay = employeeBasicCalc.Sum(x => x.DoubleLegalOTPay);
        payrollLine.DoubleLegalNDPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDPay);
        payrollLine.DoubleLegalNDOTPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDOTPay);
        payrollLine.DoubleLegalNDBasePay = employeeBasicCalc.Sum(x => x.DoubleLegalNDBasePay);
        payrollLine.DoubleLegalNDPremiumPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDPremiumPay);
        payrollLine.DoubleLegalNDOTBasePay = employeeBasicCalc.Sum(x => x.DoubleLegalNDOTBasePay);
        payrollLine.DoubleLegalOTBasePay = employeeBasicCalc.Sum(x => x.DoubleLegalOTBasePay);
        payrollLine.DoubleLegalNDOTPremiumPay = employeeBasicCalc.Sum(x => x.DoubleLegalNDOTPremiumPay);

        payrollLine.RestDoubleLegalPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalPay);
        payrollLine.RestDoubleLegalOTPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalOTPay);
        payrollLine.RestDoubleLegalNDPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDPay);
        payrollLine.RestDayNDOTPay = employeeBasicCalc.Sum(x => x.RestDayNDOTPay);
        payrollLine.RestDoubleLegalNDBasePay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDBasePay);
        payrollLine.RestDoubleLegalNDPremiumPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDPremiumPay);
        payrollLine.RestDoubleLegalNDOTBasePay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDOTBasePay);
        payrollLine.RestDoubleLegalOTBasePay = employeeBasicCalc.Sum(x => x.RestDoubleLegalOTBasePay);
        payrollLine.RestDoubleLegalNDOTPremiumPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalNDOTPremiumPay);
        payrollLine.UnpaidLeaves = employeeBasicCalc.Sum(x => x.UnpaidLeave);
        payrollLine.PaidLeaves = employeeBasicCalc.Sum(x => x.PaidLeave);

        payrollLine.HolidayPay = employeeBasicCalc.Sum(x => x.Holiday);
        payrollLine.LegalHolidayUnworkedPay = employeeBasicCalc.Sum(x => x.LegalUnWorked);
        payrollLine.RestLegalUnworkedPay = employeeBasicCalc.Sum(x => x.RestLegalUnWorked);
        payrollLine.DoubleLegalUnworkedPay = employeeBasicCalc.Sum(x => x.DoubleLegalUnworked);
        payrollLine.RestDoubleLegalUnworkedPay = employeeBasicCalc.Sum(x => x.RestDoubleLegalUnworked);

    }

    // Splits PaidLeaves by funding source using each DTR day's LeavesInfo (LeaveId + hours,
    // written by the DTR reconciliation engine) joined against Leave.PaySource. Expressed
    // as a proportional share of the already-computed PaidLeaves money (rather than
    // re-deriving hourly-rate pay here) so it can never exceed PaidLeaves and stays
    // consistent with whatever rate/proration LeavePolicy applied. Display/reporting only —
    // see NonCompanyPaidLeaves doc comment for why this must NOT be added into GrossIncome.
    internal static void ComputeNonCompanyPaidLeaves(
        List<LeaveMetaDataModel>? leaveInfo,
        Dictionary<Guid, PaySource> leavePaySourceMap,
        List<LeaveApplication>? oneTimeLeaveApplications,
        PayrollSummaryLine payrollLine)
    {
        // Needs payrollLine.PaidLeaves, just set above — display-only split, does not
        // touch GrossIncome/BasicPay (see NonCompanyPaidLeaves doc comment).
        if (leaveInfo == null || leaveInfo.Count == 0 || payrollLine.PaidLeaves == 0) return;
        var paidLeaveInfo = leaveInfo
            .Where(x => x.PayType == PayType.WithPay)
            // OneTime-payout leave days carry their FULL entitlement hours in leaveInfo (DTR
            // metadata needed for leave-credit consumption regardless of payout mode — see
            // dtr-api LeavePolicy), but contribute ZERO money to payrollLine.PaidLeaves (their
            // PaidLeaveHours is 0 — paid entirely via ApplyOneTimeLeavePayoutsToGross instead).
            // Left in, they'd skew this ratio whenever an ordinary per-day leave coexists with
            // a OneTime leave in the same period (e.g. a small vacation-leave payout getting
            // mostly attributed to an unrelated OneTime maternity leave's hours).
            .Where(x => oneTimeLeaveApplications == null || oneTimeLeaveApplications.Count == 0 ||
                !oneTimeLeaveApplications.Any(a =>
                    a.LeaveId == x.LeaveId &&
                    DateOnly.FromDateTime(x.StartDateTime) >= a.LeaveDateFrom &&
                    DateOnly.FromDateTime(x.StartDateTime) <= a.LeaveDateTo))
            .ToList();
        var totalHours = paidLeaveInfo.Sum(x => x.Hours);
        if (totalHours <= 0) return;
        var nonCompanyHours = paidLeaveInfo
            .Where(x => leavePaySourceMap.TryGetValue(x.LeaveId, out var source) && source != PaySource.Company)
            .Sum(x => x.Hours);
        payrollLine.NonCompanyPaidLeaves = payrollLine.PaidLeaves * ((decimal)nonCompanyHours / (decimal)totalHours);
    }


    private List<DTRPayModel> CalculateDTRTimePay(
        DateRangePayload payload,
        List<DailyRecordRunModel> dtrs,
        EmployeeModelPayrollRun employee,
        CalculatorPayload calcPayload)
    {

        var basicResultMoel = new List<DTRPayModel>();
        for (var date = payload.FromDate; date <= payload.ToDate; date = date.AddDays(1))
        {
            var record = SelectDtrRecordForDate(dtrs, employee.Id, date);
            if (record == null) continue;
            var context = new PayrollContextBuilder()
                .SetEmployee(employee)
                .SetDailyRecord(record)
                .SetWorkType(record)
                .SetPayrollDate(date)
                .SetPayload(calcPayload)
                .Build();

            var result = _basicPayrollCalculator.Calculate(context);
            if (result == null) continue;
            result.Date = date;
            result.DTRRef = record.BatchCode;
            result.DtrId = record.Id;
            result.ClientId = record.ClientId;
            result.DepartmentId = record.DepartmentId;
            result.PayrollGroupId = record.PayrollGroupId;
            result.SalaryType = employee.SalaryType;
            basicResultMoel.Add(result);
        }
        return basicResultMoel;
    }

    // Grouped, hours-aware pick rather than a plain FirstOrDefault: if more than one
    // DailyRecord candidate exists for this employee+date (e.g. combining multiple DTR
    // batches for a multi-client cutoff), prefer the hours-bearing one over a zero-hour
    // placeholder. Falls back to whichever record exists when every candidate for the date is
    // 0 hours -- the ordinary absent/holiday/unpaid-leave case -- so those days still get a
    // DtrId captured, not skipped. internal (not private), static (doesn't touch instance
    // state) so hrms.test can exercise this directly without a DB or the full calculator DI
    // graph — matches GetBasicPay/ComputeHoursBreakdown's established pattern below.
    internal static DailyRecordRunModel? SelectDtrRecordForDate(
        List<DailyRecordRunModel> dtrs, Guid employeeId, DateOnly date)
    {
        return dtrs
            .Where(x => x.WorkDate == date && x.EmployeeId == employeeId)
            .OrderBy(x => x.TotalHours)
            .GroupBy(x => x.WorkDate)
            .Select(g => g.FirstOrDefault(x => x.TotalHours > 0) ?? g.First())
            .FirstOrDefault();
    }

    // internal (not private), static (doesn't touch instance state) so hrms.test can exercise
    // this directly without a DB — matches ComputeThirteenthMonthTaxSplit/
    // ApplyOneTimeLeavePayoutsToGross's established pattern.
    internal static void GetBasicPay(PayrollSummaryLine payrollLine, List<DTRPayModel> TimeCalcResult,
        EmployeeModelPayrollRun employee, int oneTimeLeaveDays)
    {
        if (employee.SalaryType != SalaryType.FIXED)
        {
            payrollLine.BasicPay = TimeCalcResult.Sum(x => x.RegularDayPay);
            return;
        }
        var divisor = GetDivisor(payrollLine.PayPeriodStart, employee);
        var basicTotal = employee.MonthlyRate / divisor;
        var deductions = Math.Max(0, TimeCalcResult.Sum(x => x.LateAmount + x.UTAmount + x.UnpaidLeave + x.AbsentAmount));
        // OneTime-payout leave days (e.g. Shared-funded SSS maternity, paid via
        // ApplyOneTimeLeavePayoutsToGross's lump sum) are WithPay — not UnpaidLeave — and
        // never Late/UT/Absent (WorkType resolves to PaidLeave/GovFundedLeave for a leave
        // application, see dtr-api WorkTypeResolver), so without this term the flat monthly
        // rate silently keeps paying for them on top of the lump sum. Counts every calendar
        // day of the leave that falls in this period (including rest days — the flat rate
        // itself already implicitly covers rest days as part of the period share), applied
        // regardless of PaySource since the flat rate has no PaySource awareness at all.
        var oneTimeLeaveDeduction = Math.Max(0, oneTimeLeaveDays) * employee.DailyRate;
        basicTotal = basicTotal - deductions - oneTimeLeaveDeduction;
        payrollLine.BasicPay = Math.Max(0, basicTotal);
    }

    // Sums the calendar days of this employee's approved OneTime-payout leave application(s)
    // that fall within [periodStart, periodEnd] — see GetBasicPay. Sourced from
    // CalculatorPayload.Leaves (matched by actual leave date-range overlap, loaded every run
    // via LeaveApplicationService.FindByDateRangeAsync) rather than OneTimeLeavePayouts
    // (matched only to the single period containing ReleasePayrollDate) so the deduction
    // applies in EVERY period a multi-period leave (e.g. 105-day maternity) overlaps, not just
    // the one period that happens to release the lump sum.
    internal static int CountOneTimeLeaveCalendarDays(
        CalculatorPayload rangePayload, Guid employeeId, DateOnly periodStart, DateOnly periodEnd)
    {
        if (!rangePayload.Leaves.TryGetValue(new Leavekey(employeeId), out var apps) || apps.Count == 0)
            return 0;

        var days = 0;
        foreach (var a in apps)
        {
            if (a.PayoutMode != PayoutMode.OneTime || a.PayType == PayType.WithoutPay) continue;
            var from = a.LeaveDateFrom > periodStart ? a.LeaveDateFrom : periodStart;
            var to = a.LeaveDateTo < periodEnd ? a.LeaveDateTo : periodEnd;
            if (from > to) continue;
            days += to.DayNumber - from.DayNumber + 1;
        }
        return days;
    }

    internal static int GetDivisor(DateOnly fromDate, EmployeeModelPayrollRun employee)
    {
        if (employee.PayrollGroup == null) return 2;
        switch (employee.PayrollGroup.PayrollFrequency)
        {
            case PayrollFrequency.DAILY:
                var days = DateTime.DaysInMonth(fromDate.Year, fromDate.Month);
                return days;
            case PayrollFrequency.WEEKLY:
                return 4;
            case PayrollFrequency.SEMI_MONTHLY:
                return 2;
            case PayrollFrequency.MONTHLY:
                return 1;
            default:
                return 2;
        }
    }

    private AllowancePipeData ComputeAllowances(
        DateRangePayload payload,
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var pp = new PayrollCalcPayload(payload.FromDate, payload.ToDate, null, null, null, null);
        var context = new PayrollContextBuilder()
            .SetEmployee(employee)
            .SetPayload(rangePayload)
            .Build();

        AllowancePipeData IncomeCalcResult = _allowancesCalculator.Calculate(context);
        payrollLine.Cola = IncomeCalcResult.Cola;
        payrollLine.TotalDeminimises = IncomeCalcResult.Deminimises.Sum(x => x.Amount);
        payrollLine.TotalOtherIncome = IncomeCalcResult.OtherIncome.Sum(x => x.Amount);
        payrollLine.TotalCommissions = IncomeCalcResult.Commissions.Sum(x => x.Amount);
        payrollLine.TotalBonuses = IncomeCalcResult.Bonuses.Sum(x => x.Amount);
        payrollLine.Reimbursement = IncomeCalcResult.Reimbursements.Sum(x => x.Amount);
        payrollLine.TotalRegularAllowances = IncomeCalcResult.RegularAllowances.Sum(x => x.Amount);
        payrollLine.OtherIncomeCollection = IncomeCalcResult.AllIncome;
        payrollLine.RegularAllowanceProrated = CaptureProratedAllowance(pp, payrollLine.TotalRegularAllowances);
        payrollLine.TotalAllIncome = IncomeCalcResult.RunningTotal;
        IdentifyTaxableIncome(payrollLine, IncomeCalcResult);
        return IncomeCalcResult;
    }

    private void ComputeDeductions(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        var deductionContext = new DeductionPayloadContextBuilder()
            .SetPayload(rangePayload)
            .SetEmployee(employee)
            .SetPayrollLine(payrollLine)
            .Build();

        var deductionPipeLine = _deductionCalculator.Calculate(deductionContext);
        payrollLine.DeductionCollection = deductionPipeLine.ScheduledDeductions;
        payrollLine.NetPay = PayrollProcessorUtil.GetNetPay(payrollLine, deductionPipeLine);
        payrollLine.SSSContribution = deductionPipeLine.SSS.EE;
        payrollLine.PhilHealthContribution = deductionPipeLine.PHIC.EE;
        payrollLine.PagIbigContribution = deductionPipeLine.HDMF.EE;
        payrollLine.WithholdingTax = deductionPipeLine.TaxInfo.TaxDue;
        payrollLine.OtherDeductions = deductionPipeLine.ScheduledDeductions.Sum(x => x.Amount);
        payrollLine.TotalLoans = deductionPipeLine.ScheduledDeductions
            .Where(x => x.Type == DeductionInfoType.Loan)
            .Sum(x => x.Amount);
        payrollLine.TotalDeductions = deductionPipeLine.RunningTotal;
        payrollLine.EmployerSSSContribution = deductionPipeLine.SSS.TotalER;
        payrollLine.EmployerPhilHealthContribution = deductionPipeLine.PHIC.Total;
        payrollLine.EmployerPagIbigContribution = deductionPipeLine.HDMF.Total;
        payrollLine.EmployerECContribution = deductionPipeLine.SSS.EC;
        ApplyTaxableIncomeSplit(payrollLine);
    }

    // Populates the persisted TaxableIncome/NonTaxableIncome columns (BIR Alphalist/2316
    // rollups — see PayrollReportService.GetAlphalistAsync/Get2316DataAsync) with the same
    // formula already used for Year-End Tax Annualization
    // (TaxAnnualizationService.ComputeAsync) and PayrollOpeningBalance.DerivedTaxableIncome, so
    // all three agree on what "taxable income" means for the year. SSSContribution/
    // PhilHealthContribution/PagIbigContribution are always 0 on ThirteenthMonth/LastPay/
    // YearEndAdjustment lines, so this one formula naturally reduces to the right thing for
    // every PayrollType without per-type branches. internal (not private) so
    // ThirteenthMonthPayrollService/LastPayrollService can call it too, and so hrms.test can
    // exercise it directly without a DB.
    internal static void ApplyTaxableIncomeSplit(PayrollSummaryLine payrollLine)
    {
        var nonTaxable = payrollLine.NonTaxableBenefits
            + payrollLine.SSSContribution + payrollLine.PhilHealthContribution + payrollLine.PagIbigContribution;
        payrollLine.NonTaxableIncome = nonTaxable;
        payrollLine.TaxableIncome = Math.Max(0, payrollLine.GrossIncome - nonTaxable);
    }

    // Injects an approved OneTime leave payout (see LeaveApplication.PayoutMode) matched to
    // this run via ReleasePayrollDate. GovernmentAmount is deliberately kept out of
    // GrossIncome — it's a government benefit pass-through, not compensation, and all four
    // statutory calculators (SSS/PHIC/HDMF/WTax) share the same GrossIncome-based bracket
    // lookup, so there's no cheaper way to exempt it from WTax alone. CompanyAmount is taxable
    // compensation, so it's added to GrossIncome here and (see the call site) NetPay is left
    // for ComputeDeductions to (re)compute from that.
    //
    // GovernmentFundedLeavePay/NonTaxableBenefits always accumulate the FULL entitlement
    // regardless of who actually disburses it — that stays informational, for payslip
    // visibility of the benefit. But only the portion the EMPLOYER actually advances through
    // this payroll run belongs in NetPay: when the government pays the employee directly (the
    // exception case — e.g. the employee separated before the SSS claim was filed), this
    // employer never hands that money over, so it must not inflate this run's NetPay. The
    // return value is that employer-advanced portion; the caller adds it to NetPay separately,
    // after ComputeDeductions runs. Per-payout, not aggregate, because one employee can have
    // multiple OneTime payouts in the same period with different disbursement methods.
    // internal (not private) so hrms.test can exercise this directly without a DB — see
    // Hrms.Core's InternalsVisibleTo for hrms.test.
    internal static decimal ApplyOneTimeLeavePayoutsToGross(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        if (!rangePayload.OneTimeLeavePayouts.TryGetValue(new EmployeeKey(employee.Id), out var payouts))
            return 0m;

        var employerAdvancedGovPay = 0m;
        var breakdownParts = new List<string>();
        foreach (var payout in payouts)
        {
            var gov = payout.GovernmentAmount ?? 0;
            var comp = payout.CompanyAmount ?? 0;
            payrollLine.GovernmentFundedLeavePay += gov;
            payrollLine.CompanyFundedLeavePay += comp;
            payrollLine.NonTaxableBenefits += gov;
            payrollLine.TaxableBenefits += comp;
            // GrossIncome itself is NOT touched here — it's unconditionally recomputed by
            // PayrollProcessorUtil.GetGross() right after this method returns (see the call
            // site), which already folds in CompanyFundedLeavePay (incremented above) and
            // deliberately excludes GovernmentFundedLeavePay.

            // Null = inherit the leave type's default (LeaveApplication.EmployerAdvancesPayment
            // doc comment) — payout.Leave is already loaded via LoadOneTimePayoutsAsync's
            // .Include(x => x.Leave).
            var employerAdvances = payout.EmployerAdvancesPayment ?? payout.Leave.EmployerAdvancesPayment;
            if (employerAdvances) employerAdvancedGovPay += gov;

            var leaveName = payout.Leave.Description;
            var govLabel = employerAdvances ? "Government — Employer Advance" : "Government — Direct Deposit";
            breakdownParts.Add(gov > 0 && comp > 0
                ? $"{leaveName}: {comp:0.00} (Company) + {gov:0.00} ({govLabel})"
                : comp > 0
                    ? $"{leaveName}: {comp:0.00} (Company)"
                    : $"{leaveName}: {gov:0.00} ({govLabel})");
        }
        payrollLine.OneTimePayoutBreakdown = breakdownParts.Count == 0 ? null : string.Join("; ", breakdownParts);
        return employerAdvancedGovPay;
    }

    // internal (not private) so LastPayrollService can reuse this exact logic for its
    // HR-confirmed Salary Adjustments, and so hrms.test can exercise it directly without a DB.
    internal static void ApplySalaryAdjustments(
        CalculatorPayload rangePayload,
        EmployeeModelPayrollRun employee,
        PayrollSummaryLine payrollLine)
    {
        if (!rangePayload.SalaryAdjustments.TryGetValue(new EmployeeKey(employee.Id), out var adjustments))
            return;

        foreach (var adj in adjustments)
        {
            //TODO add other salary type adjustment here
            switch (adj.AdjustmentType)
            {
                case SalaryAdjustmentType.Salary:
                    payrollLine.BasicPay += adj.Amount;
                    payrollLine.GrossIncome += adj.Amount;
                    payrollLine.NetPay += adj.Amount;
                    break;
                case SalaryAdjustmentType.Allowance:
                    payrollLine.TotalOtherIncome += adj.Amount;
                    payrollLine.GrossIncome += adj.Amount;
                    payrollLine.NetPay += adj.Amount;
                    break;
                case SalaryAdjustmentType.Deduction:
                    payrollLine.OtherDeductions += adj.Amount;
                    payrollLine.TotalDeductions += adj.Amount;
                    payrollLine.NetPay -= adj.Amount;
                    break;
            }
        }
    }

    private List<ProratedAllowanceModel> CaptureProratedAllowance(PayrollCalcPayload payload, decimal regularAllowance)
    {

        var result = new List<ProratedAllowanceModel>();
        var allDates = new List<DateOnly>();
        // Collect all dates in the range
        for (var date = payload.FromDate; date <= payload.ToDate; date = date.AddDays(1))
        {
            allDates.Add(date);
        }

        // Total days covered in the entire range
        int totalDays = allDates.Count;

        // Daily rate based on total allowance
        decimal dailyRate = regularAllowance / totalDays;

        // Group by month/year and compute prorated allowance
        var monthDays = allDates
            .GroupBy(x => new MonthYear(x.Month, x.Year))
            .Select(g => new { Key = g.Key, DayCount = g.Count() })
            .ToList();

        foreach (var item in monthDays)
        {
            result.Add(new ProratedAllowanceModel
            {
                Month = item.Key.Month,
                Year = item.Key.Year,
                Amount = dailyRate * item.DayCount // prorated value for that month
            });
        }
        return result;
    }
    private void IdentifyTaxableIncome(PayrollSummaryLine payrollLine, AllowancePipeData incomeInfo)
    {
        var allincome = incomeInfo.AllIncome;

        payrollLine.NonTaxableBenefits = allincome
            .Where(x => !x.Taxable)
            .Sum(x => x.Amount);

        payrollLine.TaxableBenefits = allincome
            .Where(x => x.Taxable)
            .Sum(x => x.Amount);
    }
}

public class ProratedAllowanceModel
{
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Amount { get; set; }
}

public record MonthYear(int Month, int Year);
