using Hrms.Core.Services;
using Hrms.Domain.Entities;

namespace hrms.test.ServiceTests;

/// <summary>
/// LeaveEligibilityCalculator — the shared branch (LeaveApplicationService.EnsurePolicyAsync
/// and LeavePeriodGrantWorker) between Leave.EligibilityBasis.TenureMonths (months since hire)
/// and .PresentDays (count of posted DTR days that aren't Absent/Incomplete/Skipped).
/// </summary>
public class LeaveEligibilityCalculatorTests
{
    private static Leave BuildLeave(LeaveEligibilityBasis basis, int minServiceMonths = 0, int minPresentDays = 0) => new()
    {
        Description = "Test Leave",
        EligibilityBasis = basis,
        MinServiceMonths = minServiceMonths,
        MinPresentDays = minPresentDays,
    };

    [Theory]
    [InlineData(2026, 1, 15, 2026, 6, 10, 4)]  // day-of-month hasn't come around again -> 4, not 5
    [InlineData(2026, 1, 15, 2026, 7, 15, 6)]  // exact day-of-month match -> full 6
    [InlineData(2026, 1, 15, 2025, 12, 1, 0)]  // "to" before "from" -> clamped to 0
    public void MonthsBetween_ComputesWholeElapsedMonths(
        int fromYear, int fromMonth, int fromDay,
        int toYear, int toMonth, int toDay,
        int expected)
    {
        var from = new DateOnly(fromYear, fromMonth, fromDay);
        var to = new DateOnly(toYear, toMonth, toDay);

        LeaveEligibilityCalculator.MonthsBetween(from, to).Should().Be(expected);
    }

    [Fact]
    public void IsServiceRequirementMet_TenureBasis_UsesMonthsServed()
    {
        var leave = BuildLeave(LeaveEligibilityBasis.TenureMonths, minServiceMonths: 6);

        LeaveEligibilityCalculator.IsServiceRequirementMet(leave, monthsServed: 5, presentDays: 999).Should().BeFalse();
        LeaveEligibilityCalculator.IsServiceRequirementMet(leave, monthsServed: 6, presentDays: 0).Should().BeTrue();
    }

    [Fact]
    public void IsServiceRequirementMet_PresentDaysBasis_UsesPresentDays()
    {
        var leave = BuildLeave(LeaveEligibilityBasis.PresentDays, minPresentDays: 30);

        LeaveEligibilityCalculator.IsServiceRequirementMet(leave, monthsServed: 999, presentDays: 29).Should().BeFalse();
        LeaveEligibilityCalculator.IsServiceRequirementMet(leave, monthsServed: 0, presentDays: 30).Should().BeTrue();
    }

    [Fact]
    public void ServiceRequirementMessage_MentionsTheBasisThatWasActuallyEvaluated()
    {
        var tenureLeave = BuildLeave(LeaveEligibilityBasis.TenureMonths, minServiceMonths: 12);
        var presentDaysLeave = BuildLeave(LeaveEligibilityBasis.PresentDays, minPresentDays: 260);

        LeaveEligibilityCalculator.ServiceRequirementMessage(tenureLeave, monthsServed: 3, presentDays: 0)
            .Should().Contain("month").And.Contain("12");
        LeaveEligibilityCalculator.ServiceRequirementMessage(presentDaysLeave, monthsServed: 0, presentDays: 100)
            .Should().Contain("present day").And.Contain("260");
    }

    [Fact]
    public void NonPresentWorkTypes_ExcludesOnlyGenuineServiceGaps()
    {
        LeaveEligibilityCalculator.NonPresentWorkTypes.Should().BeEquivalentTo(
            new[] { WorkType.Absent, WorkType.Incomplete, WorkType.Skipped });
    }
}
