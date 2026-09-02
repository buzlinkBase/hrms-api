using Hrms.Core.Services;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;

namespace hrms.test.PayrollRunTests;

/// <summary>
/// PayrollProcessorService.ComputeHoursBreakdown / BuildPaidLeaveBreakdown /
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

        PayrollProcessorService.ComputeHoursBreakdown(dtrs, line);

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

        PayrollProcessorService.ComputeHoursBreakdown(dtrs, line);

        line.OvertimeHours.Should().Be(6); // 2 + 1 + 3 — the *NDOTHours columns are separate and not summed in
    }

    [Fact]
    public void ComputeHoursBreakdown_EmptyDtrList_LeavesEveryFieldAtZero()
    {
        var line = new PayrollSummaryLine();

        PayrollProcessorService.ComputeHoursBreakdown(new List<DailyRecordRunModel>(), line);

        line.RegularNetHours.Should().Be(0);
        line.OvertimeHours.Should().Be(0);
        line.PaidLeaveHours.Should().Be(0);
        line.UnpaidLeaveHours.Should().Be(0);
    }

    // --- BuildPaidLeaveBreakdown ------------------------------------------------------------

    [Fact]
    public void BuildPaidLeaveBreakdown_NullOrEmptyLeaveInfo_ReturnsNull()
    {
        PayrollProcessorService.BuildPaidLeaveBreakdown(null).Should().BeNull();
        PayrollProcessorService.BuildPaidLeaveBreakdown(new List<LeaveMetaDataModel>()).Should().BeNull();
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

        var result = PayrollProcessorService.BuildPaidLeaveBreakdown(leaveInfo);

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

        var result = PayrollProcessorService.BuildPaidLeaveBreakdown(leaveInfo);

        result.Should().Contain("Vacation Leave: 8h (Paid)");
        result.Should().Contain("Vacation Leave: 4h (Unpaid)");
    }

    // --- ComputeNonCompanyPaidLeaves --------------------------------------------------------

    [Fact]
    public void ComputeNonCompanyPaidLeaves_NoLeaveInfoOrZeroPaidLeaves_LeavesFieldAtZero()
    {
        var line = new PayrollSummaryLine { PaidLeaves = 5_000 };
        PayrollProcessorService.ComputeNonCompanyPaidLeaves(null, new Dictionary<Guid, PaySource>(), line);
        line.NonCompanyPaidLeaves.Should().Be(0);

        var lineZeroPaid = new PayrollSummaryLine { PaidLeaves = 0 };
        var leaveInfo = new List<LeaveMetaDataModel> { new() { LeaveId = Guid.NewGuid(), Hours = 8, PayType = PayType.WithPay } };
        PayrollProcessorService.ComputeNonCompanyPaidLeaves(leaveInfo, new Dictionary<Guid, PaySource>(), lineZeroPaid);
        lineZeroPaid.NonCompanyPaidLeaves.Should().Be(0);
    }

    [Fact]
    public void ComputeNonCompanyPaidLeaves_AllCompanyFunded_LeavesNonCompanyShareAtZero()
    {
        var leaveId = Guid.NewGuid();
        var leaveInfo = new List<LeaveMetaDataModel> { new() { LeaveId = leaveId, Hours = 8, PayType = PayType.WithPay } };
        var paySourceMap = new Dictionary<Guid, PaySource> { [leaveId] = PaySource.Company };
        var line = new PayrollSummaryLine { PaidLeaves = 4_000 };

        PayrollProcessorService.ComputeNonCompanyPaidLeaves(leaveInfo, paySourceMap, line);

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

        PayrollProcessorService.ComputeNonCompanyPaidLeaves(leaveInfo, paySourceMap, line);

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

        PayrollProcessorService.ComputeNonCompanyPaidLeaves(leaveInfo, paySourceMap, line);

        line.NonCompanyPaidLeaves.Should().Be(0); // only the 8 Company-sourced hours counted -> 0% non-company
    }

    [Fact]
    public void ComputeNonCompanyPaidLeaves_LeaveIdMissingFromPaySourceMap_TreatedAsCompanyFunded()
    {
        var unknownLeaveId = Guid.NewGuid();
        var leaveInfo = new List<LeaveMetaDataModel> { new() { LeaveId = unknownLeaveId, Hours = 8, PayType = PayType.WithPay } };
        var line = new PayrollSummaryLine { PaidLeaves = 2_000 };

        PayrollProcessorService.ComputeNonCompanyPaidLeaves(leaveInfo, new Dictionary<Guid, PaySource>(), line);

        // TryGetValue failing short-circuits the `&&` in the Where clause, excluding the hours
        // from the non-company numerator entirely — an unmapped LeaveId defaults to
        // Company-funded (fail-safe) rather than inflating the non-company split.
        line.NonCompanyPaidLeaves.Should().Be(0);
    }
}
