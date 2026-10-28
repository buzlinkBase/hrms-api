using Hrms.Core.Pipelines;
using Hrms.Core.Policies.DTRPolicies;
using Hrms.Core.Services;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// EmployeePayrollLineService.ComputeHoursBreakdown / BuildPaidLeaveBreakdown /
/// ComputeNonCompanyPaidLeaves — pure per-employee aggregation run once per payroll line
/// inside GenerateAsync. Made `internal` (not private) specifically so these can be tested
/// here without a database, matching OneTimeLeavePayoutTests' convention.
/// </summary>
public class PayrollLineComputationTests : TestContextBase
{
    private static DailyRecordRunModel Dtr(
        double regularNetHours = 0,
        double regularOTHours = 0,
        double restDayOTHours = 0,
        double legalHolOTHours = 0,
        double obHours = 0,
        double paidLeaveHours = 0,
        double unpaidLeaveHours = 0) => new()
        {
            RegularNetHours = regularNetHours,
            RegularOTHours = regularOTHours,
            RestDayOTHours = restDayOTHours,
            LegalHolOTHours = legalHolOTHours,
            OBHours = obHours,
            PaidLeaveHours = paidLeaveHours,
            UnpaidLeaveHours = unpaidLeaveHours,
        };

    // --- ComputeHoursBreakdown -----------------------------------------------------------

    [Fact]
    public void ComputeHoursBreakdown_SumsEachCategoryAcrossAllDaysInThePeriod()
    {
        var dtrs = new List<DailyRecordRunModel>
        {
            Dtr(regularNetHours: 8, obHours: 1, paidLeaveHours: 0),
            Dtr(regularNetHours: 8, obHours: 2, paidLeaveHours: 8),
        };
        var line = new PayrollSummaryLine();

        EmployeePayrollLineService.ComputeHoursBreakdown(dtrs, line);

        line.RegularNetHours.Should().Be(16);
        line.OBHours.Should().Be(3);
        line.PaidLeaveHours.Should().Be(8);
    }

    [Fact]
    public void ComputeHoursBreakdown_OvertimeHours_SumsOnlyThePureOTCategories_ExcludingNDOTCombos()
    {
        var dtrs = new List<DailyRecordRunModel>
        {
            Dtr(regularOTHours: 2, restDayOTHours: 1, legalHolOTHours: 3),
        };
        var line = new PayrollSummaryLine();

        EmployeePayrollLineService.ComputeHoursBreakdown(dtrs, line);

        line.OvertimeHours.Should().Be(6); // 2 + 1 + 3 — OT is natural (already includes NDOT), so *NDOTHours is never added on top
    }

    [Fact]
    public void ComputeHoursBreakdown_EmptyDtrList_LeavesEveryFieldAtZero()
    {
        var line = new PayrollSummaryLine();

        EmployeePayrollLineService.ComputeHoursBreakdown(new List<DailyRecordRunModel>(), line);

        line.RegularNetHours.Should().Be(0);
        line.OvertimeHours.Should().Be(0);
        line.PaidLeaveHours.Should().Be(0);
        line.UnpaidLeaveHours.Should().Be(0);
    }

    // --- BuildPaidLeaveBreakdown ------------------------------------------------------------

    [Fact]
    public void BuildPaidLeaveBreakdown_NullOrEmptyLeaveInfo_ReturnsNull()
    {
        EmployeePayrollLineService.BuildPaidLeaveBreakdown(null).Should().BeNull();
        EmployeePayrollLineService.BuildPaidLeaveBreakdown(new List<LeaveMetaDataModel>()).Should().BeNull();
    }

    [Fact]
    public void BuildPaidLeaveBreakdown_GroupsByLeaveNameAndPayType_SummingHours()
    {
        var leaveInfo = new List<LeaveMetaDataModel>
        {
            new() { Name = "Sick Leave", Hours = 4, PayType = PayType.WithPay },
            new() { Name = "Sick Leave", Hours = 4, PayType = PayType.WithPay },
            new() { Name = "Vacation Leave", Hours = 8, PayType = PayType.WithoutPay },
        };

        var result = EmployeePayrollLineService.BuildPaidLeaveBreakdown(leaveInfo);

        result.Should().Contain("Sick Leave: 8h (Paid)");
        result.Should().Contain("Vacation Leave: 8h (Unpaid)");
    }

    [Fact]
    public void BuildPaidLeaveBreakdown_SameLeave_SplitAcrossPaidAndUnpaidDays_ProducesTwoSeparateEntries()
    {
        // e.g. balance ran out mid-period — same leave type shows once as Paid, once as Unpaid.
        var leaveInfo = new List<LeaveMetaDataModel>
        {
            new() { Name = "Vacation Leave", Hours = 8, PayType = PayType.WithPay },
            new() { Name = "Vacation Leave", Hours = 4, PayType = PayType.WithoutPay },
        };

        var result = EmployeePayrollLineService.BuildPaidLeaveBreakdown(leaveInfo);

        result.Should().Contain("Vacation Leave: 8h (Paid)");
        result.Should().Contain("Vacation Leave: 4h (Unpaid)");
    }

    // --- ComputeNonCompanyPaidLeaves --------------------------------------------------------

    [Fact]
    public void ComputeNonCompanyPaidLeaves_NoLeaveInfoOrZeroPaidLeaves_LeavesFieldAtZero()
    {
        var line = new PayrollSummaryLine { PaidLeaves = 5_000 };
        EmployeePayrollLineService.ComputeNonCompanyPaidLeaves(null, new Dictionary<Guid, PaySource>(), null, line);
        line.NonCompanyPaidLeaves.Should().Be(0);

        var lineZeroPaid = new PayrollSummaryLine { PaidLeaves = 0 };
        var leaveInfo = new List<LeaveMetaDataModel> { new() { LeaveId = Guid.NewGuid(), Hours = 8, PayType = PayType.WithPay } };
        EmployeePayrollLineService.ComputeNonCompanyPaidLeaves(leaveInfo, new Dictionary<Guid, PaySource>(), null, lineZeroPaid);
        lineZeroPaid.NonCompanyPaidLeaves.Should().Be(0);
    }

    [Fact]
    public void ComputeNonCompanyPaidLeaves_AllCompanyFunded_LeavesNonCompanyShareAtZero()
    {
        var leaveId = Guid.NewGuid();
        var leaveInfo = new List<LeaveMetaDataModel> { new() { LeaveId = leaveId, Hours = 8, PayType = PayType.WithPay } };
        var paySourceMap = new Dictionary<Guid, PaySource> { [leaveId] = PaySource.Company };
        var line = new PayrollSummaryLine { PaidLeaves = 4_000 };

        EmployeePayrollLineService.ComputeNonCompanyPaidLeaves(leaveInfo, paySourceMap, null, line);

        line.NonCompanyPaidLeaves.Should().Be(0);
    }

    [Fact]
    public void ComputeNonCompanyPaidLeaves_SplitsPaidLeavesMoneyProportionallyByHours_ForNonCompanySources()
    {
        var companyLeaveId = Guid.NewGuid();
        var govLeaveId = Guid.NewGuid();
        var leaveInfo = new List<LeaveMetaDataModel>
        {
            new() { LeaveId = companyLeaveId, Hours = 6, PayType = PayType.WithPay },
            new() { LeaveId = govLeaveId, Hours = 2, PayType = PayType.WithPay },
        };
        var paySourceMap = new Dictionary<Guid, PaySource>
        {
            [companyLeaveId] = PaySource.Company,
            [govLeaveId] = PaySource.Government,
        };
        var line = new PayrollSummaryLine { PaidLeaves = 4_000 };

        EmployeePayrollLineService.ComputeNonCompanyPaidLeaves(leaveInfo, paySourceMap, null, line);

        line.NonCompanyPaidLeaves.Should().Be(1_000); // 4,000 * (2/8) — only the Government-sourced hours' share
    }

    [Fact]
    public void ComputeNonCompanyPaidLeaves_IgnoresUnpaidLeaveDays_WhenComputingTheHoursSplit()
    {
        var companyLeaveId = Guid.NewGuid();
        var govLeaveId = Guid.NewGuid();
        var leaveInfo = new List<LeaveMetaDataModel>
        {
            new() { LeaveId = companyLeaveId, Hours = 8, PayType = PayType.WithPay },
            new() { LeaveId = govLeaveId, Hours = 8, PayType = PayType.WithoutPay }, // excluded: not WithPay
        };
        var paySourceMap = new Dictionary<Guid, PaySource>
        {
            [companyLeaveId] = PaySource.Company,
            [govLeaveId] = PaySource.Government,
        };
        var line = new PayrollSummaryLine { PaidLeaves = 4_000 };

        EmployeePayrollLineService.ComputeNonCompanyPaidLeaves(leaveInfo, paySourceMap, null, line);

        line.NonCompanyPaidLeaves.Should().Be(0); // only the 8 Company-sourced hours counted -> 0% non-company
    }

    [Fact]
    public void ComputeNonCompanyPaidLeaves_LeaveIdMissingFromPaySourceMap_TreatedAsCompanyFunded()
    {
        var unknownLeaveId = Guid.NewGuid();
        var leaveInfo = new List<LeaveMetaDataModel> { new() { LeaveId = unknownLeaveId, Hours = 8, PayType = PayType.WithPay } };
        var line = new PayrollSummaryLine { PaidLeaves = 2_000 };

        EmployeePayrollLineService.ComputeNonCompanyPaidLeaves(leaveInfo, new Dictionary<Guid, PaySource>(), null, line);

        // TryGetValue failing short-circuits the `&&` in the Where clause, excluding the hours
        // from the non-company numerator entirely — an unmapped LeaveId defaults to
        // Company-funded (fail-safe) rather than inflating the non-company split.
        line.NonCompanyPaidLeaves.Should().Be(0);
    }

    // --- Day client vs home client (per-client runs) -----------------------------------------

    [Fact]
    public void ComputeRetirementAccrual_PerClientRun_AccruesEachDayUnderTheClientItWasWorkedFor()
    {
        var home = Guid.NewGuid();
        var site = Guid.NewGuid();
        var employee = new EmployeeModelPayrollRun { ClientId = home, DailyRate = 800m };
        var payload = new CalculatorPayload
        {
            ClientRetirementDaysPerYear = new Dictionary<Guid, decimal?> { [home] = 15m, [site] = 30m },
            CompanyPolicy = new CompanyPolicyRule { DeductionCarrierMode = DeductionCarrierMode.PrimaryClient },
        };
        var days = new List<DailyRecordRunModel>
        {
            new() { ClientId = home, RegularNetHours = 120 },
            new() { ClientId = site, RegularNetHours = 120 },
        };
        var line = new PayrollSummaryLine { RegularNetHours = 240 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line, days);

        // 100 x (120 x 15 / 360) + 100 x (120 x 30 / 360) = 500 + 1,000
        line.RetirementAccrual.Should().Be(1_500m);
    }

    [Fact]
    public void ComputeRetirementAccrual_CarrierModeOff_KeepsEveryHourUnderTheHomeClient()
    {
        var home = Guid.NewGuid();
        var site = Guid.NewGuid();
        var employee = new EmployeeModelPayrollRun { ClientId = home, DailyRate = 800m };
        var payload = new CalculatorPayload
        {
            ClientRetirementDaysPerYear = new Dictionary<Guid, decimal?> { [home] = 15m, [site] = 30m },
        };
        var days = new List<DailyRecordRunModel>
        {
            new() { ClientId = site, RegularNetHours = 240 },
        };
        var line = new PayrollSummaryLine { RegularNetHours = 240 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line, days);

        // 100 x (240 x 15 / 360) = 1,000 -- the legacy formula, untouched by the day's client.
        line.RetirementAccrual.Should().Be(1_000m);
    }

    [Theory]
    [InlineData(DeductionCarrierMode.EveryRun, true)]
    [InlineData(DeductionCarrierMode.PrimaryClient, false)]
    public void PayDayClient_FollowsTheDayOnlyInAPerClientRun(DeductionCarrierMode mode, bool expectHome)
    {
        var home = Guid.NewGuid();
        var site = Guid.NewGuid();
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { ClientId = home },
            DailyRecord = new DailyRecordRunModel { ClientId = site },
            Payload = new CalculatorPayload { CompanyPolicy = new CompanyPolicyRule { DeductionCarrierMode = mode } },
        };

        PayDayClient.For(context).Should().Be(expectHome ? home : site);
    }

    [Fact]
    public void ClientPremiumRateHandler_PerClientRun_UsesTheDayClientsOverrideNotTheHomeClients()
    {
        var home = Guid.NewGuid();
        var site = Guid.NewGuid();
        var context = new PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { ClientId = home },
            DailyRecord = new DailyRecordRunModel { ClientId = site },
            Payload = new CalculatorPayload
            {
                CompanyPolicy = new CompanyPolicyRule { DeductionCarrierMode = DeductionCarrierMode.MostDays },
                ClientPremiumRates = new Dictionary<ClientRateKey, decimal>
                {
                    [new ClientRateKey(home, RateType.NIGHTDIFF)] = 1.5m,
                    [new ClientRateKey(site, RateType.NIGHTDIFF)] = 1.25m,
                },
            },
        };

        new ClientPremiumRateHandler().TryResolve(context, RateType.NIGHTDIFF, out var rate).Should().BeTrue();
        rate.Should().Be(1.25m);
    }

    // --- ComputeRetirementAccrual -----------------------------------------------------------
    // Setup > Client > Settings > Allowances > Retirement (days/year). Formula from the client:
    // (dailyRate / 8) x (regularNetHours x retirementDaysPerYear / 12 / 30).

    [Fact]
    public void ComputeRetirementAccrual_ClientHasRetirementDaysConfigured_ComputesTheFormula()
    {
        var clientId = Guid.NewGuid();
        var employee = new EmployeeModelPayrollRun { ClientId = clientId, DailyRate = 800m };
        var payload = new CalculatorPayload
        {
            ClientRetirementDaysPerYear = new Dictionary<Guid, decimal?> { [clientId] = 22.5m },
        };
        var line = new PayrollSummaryLine { RegularNetHours = 176 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line);

        // (800/8) x (176 x 22.5 / 12 / 30) = 100 x 11 = 1,100
        line.RetirementAccrual.Should().Be(1_100m);
    }

    [Fact]
    public void ComputeRetirementAccrual_ClientConfiguredWithNoRetirementBenefit_LeavesAccrualAtZero()
    {
        var clientId = Guid.NewGuid();
        var employee = new EmployeeModelPayrollRun { ClientId = clientId, DailyRate = 800m };
        var payload = new CalculatorPayload
        {
            // key present but null — "no retirement benefit" for this client, per the spreadsheet.
            ClientRetirementDaysPerYear = new Dictionary<Guid, decimal?> { [clientId] = null },
        };
        var line = new PayrollSummaryLine { RegularNetHours = 176 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line);

        line.RetirementAccrual.Should().Be(0);
    }

    [Fact]
    public void ComputeRetirementAccrual_ClientIdNotInLookup_LeavesAccrualAtZero()
    {
        var employee = new EmployeeModelPayrollRun { ClientId = Guid.NewGuid(), DailyRate = 800m };
        var payload = new CalculatorPayload(); // empty lookup — client not represented at all
        var line = new PayrollSummaryLine { RegularNetHours = 176 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line);

        line.RetirementAccrual.Should().Be(0);
    }

    [Fact]
    public void ComputeRetirementAccrual_EmployeeHasNoClient_LeavesAccrualAtZero()
    {
        var employee = new EmployeeModelPayrollRun { ClientId = null, DailyRate = 800m };
        var payload = new CalculatorPayload();
        var line = new PayrollSummaryLine { RegularNetHours = 176 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line);

        line.RetirementAccrual.Should().Be(0);
    }

    [Fact]
    public void ComputeRetirementAccrual_ZeroRegularHours_ContributesNothing()
    {
        var clientId = Guid.NewGuid();
        var employee = new EmployeeModelPayrollRun { ClientId = clientId, DailyRate = 800m };
        var payload = new CalculatorPayload
        {
            ClientRetirementDaysPerYear = new Dictionary<Guid, decimal?> { [clientId] = 22.5m },
        };
        var line = new PayrollSummaryLine { RegularNetHours = 0 };

        EmployeePayrollLineService.ComputeRetirementAccrual(employee, payload, line);

        line.RetirementAccrual.Should().Be(0);
    }
}
