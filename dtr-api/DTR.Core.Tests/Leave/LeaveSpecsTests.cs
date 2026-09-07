using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// IsLeaved / IsLeaveWithPay / IsGovFundedLeaved — the three Leave-related rule specifications.
/// IsGovFundedLeaved is also exercised indirectly through WorkTypeResolverTests' GovFundedLeave
/// scenario; these are the direct, isolated equivalents plus the negative cases.
/// </summary>
public class LeaveSpecsTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);
    private static readonly DateOnly Day = new(2026, 1, 5);

    // --- IsLeaved -----------------------------------------------------------------------------
    // Despite the name, this only checks CurrentLeaves != null -- not "has any leave application"
    // -- so it's true even for an employee with zero leave applications on file.

    [Fact]
    public void IsLeaved_CurrentLeavesIsAnEmptyList_StillReturnsTrue()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        new IsLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeTrue();
    }

    [Fact]
    public void IsLeaved_CurrentLeavesIsNull_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves = null!;
        new IsLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    // --- IsLeaveWithPay -------------------------------------------------------------------------
    // Reads Provider.LeaveProvider.GetApplications(date) -- a separately-seeded source from
    // Data.CurrentLeaves (ApplyLeave wires both to keep them consistent in tests).

    [Fact]
    public void IsLeaveWithPay_HasAWithPayApplicationForTheDate_ReturnsTrue()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, payType: PayType.WithPay, fromDate: Day));

        new IsLeaveWithPay(Day).IsSatisfiedBy(TimeRange.Empty, context).Should().BeTrue();
    }

    [Fact]
    public void IsLeaveWithPay_OnlyWithoutPayApplications_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, payType: PayType.WithoutPay, fromDate: Day));

        new IsLeaveWithPay(Day).IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    [Fact]
    public void IsLeaveWithPay_NoApplicationsForTheDate_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        new IsLeaveWithPay(Day).IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    // --- IsGovFundedLeaved ----------------------------------------------------------------------

    [Fact]
    public void IsGovFundedLeaved_GovernmentSourceOneTimeWithPay_ReturnsTrue()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, paySource: PaySource.Government, payoutMode: PayoutMode.OneTime, payType: PayType.WithPay, fromDate: Day));

        new IsGovFundedLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeTrue();
    }

    [Fact]
    public void IsGovFundedLeaved_SharedSourceOneTimeWithPay_ReturnsTrue()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, paySource: PaySource.Shared, payoutMode: PayoutMode.OneTime, payType: PayType.WithPay, fromDate: Day));

        new IsGovFundedLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeTrue();
    }

    [Fact]
    public void IsGovFundedLeaved_CompanySourced_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, paySource: PaySource.Company, payoutMode: PayoutMode.OneTime, payType: PayType.WithPay, fromDate: Day));

        new IsGovFundedLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    [Fact]
    public void IsGovFundedLeaved_PerDayPayoutMode_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, paySource: PaySource.Government, payoutMode: PayoutMode.PerDay, payType: PayType.WithPay, fromDate: Day));

        new IsGovFundedLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    [Fact]
    public void IsGovFundedLeaved_WithoutPay_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyLeave(context, BuildLeaveApplication(DurationType.SingleDay, paySource: PaySource.Government, payoutMode: PayoutMode.OneTime, payType: PayType.WithoutPay, fromDate: Day));

        new IsGovFundedLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }

    [Fact]
    public void IsGovFundedLeaved_NoLeaves_ReturnsFalse()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        new IsGovFundedLeaved().IsSatisfiedBy(TimeRange.Empty, context).Should().BeFalse();
    }
}
