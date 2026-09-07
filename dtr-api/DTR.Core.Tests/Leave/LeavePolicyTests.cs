using DTR.Core.DTR.Rules.Policies;
using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;

namespace DTR.Core.Tests.LeaveSubsystem;

/// <summary>
/// LeavePolicy — iterates CurrentLeaves, skips WithoutPay applications outright, computes each
/// remaining one's TimeRange via LeaveTimeRangeStrategyFactory (always
/// TimeRangeLeaveTimeRangeStrategy today), records a "leave{ApplicationId}" ledger tag and
/// accumulates it into the returned range for anything not OneTime-payout, and always builds a
/// "PaidLeave" metadata entry -- except that OneTime/Government-sourced applications never
/// reach the metadata step at all, because TimeRangeLeaveTimeRangeStrategy's own eligibility
/// gate (VirtualTimeComposer.IsEligibleForVirtualAttendance) already computes them to Empty,
/// which LeavePolicy's `if (result.IsEmpty()) continue;` skips before the metadata is built.
/// That's a real mismatch with this class's own doc comment (which describes OneTime leaves as
/// still contributing metadata) -- documented here as current behavior, not fixed.
/// </summary>
public class LeavePolicyTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);

    private class AlwaysTrueSpec : IRuleSpecification
    {
        public bool IsSatisfiedBy(TimeRange input, TimeContext context) => true;
    }

    private class AlwaysFalseSpec : IRuleSpecification
    {
        public bool IsSatisfiedBy(TimeRange input, TimeContext context) => false;
    }

    private static TimeContext BuildContext()
    {
        var context = CreateContext(ShiftStart, ShiftEnd, maxWorkingMinutes: 540);
        context.Payload.Ledger.RecordByTag("work_time", context, Range(ShiftStart, ShiftEnd));
        return context;
    }

    [Fact]
    public void NoLeaveApplications_ReturnsEmpty()
    {
        var context = BuildContext();

        var result = new LeavePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void SpecNotSatisfied_ReturnsEmpty_EvenWithApplications()
    {
        var context = BuildContext();
        ApplyLeave(context, BuildLeaveApplication(DurationType.Partial,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0)));

        var result = new LeavePolicy(new AlwaysFalseSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void SingleEligiblePaidApplication_ReturnsItsRangeAndRecordsMetadataAndLedgerTag()
    {
        var context = BuildContext();
        var leave = BuildLeaveApplication(DurationType.Partial,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0));
        ApplyLeave(context, leave);

        var result = new LeavePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(120);
        context.Payload.Ledger.GetByTag("leave" + leave.Id, context).TotalMinutes.Should().Be(120);

        var metas = result.GetMetaData<List<LeaveMetaDataModel>>("PaidLeave");
        metas.Should().ContainSingle();
        metas![0].LeaveId.Should().Be(leave.LeaveId);
        metas[0].Hours.Should().Be(2);
        metas[0].PayType.Should().Be(PayType.WithPay);
    }

    [Fact]
    public void WithoutPayApplication_SkippedBeforeStrategyEverRuns_NoLedgerTagEither()
    {
        var context = BuildContext();
        var leave = BuildLeaveApplication(DurationType.Partial, payType: PayType.WithoutPay,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0));
        ApplyLeave(context, leave);

        var result = new LeavePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
        context.Payload.Ledger.GetByTag("leave" + leave.Id, context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void OneTimePayoutApplication_ContributesNeitherHoursNorMetadata_ContradictsItsOwnDocComment()
    {
        var context = BuildContext();
        var leave = BuildLeaveApplication(DurationType.Partial, payoutMode: PayoutMode.OneTime,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0));
        ApplyLeave(context, leave);

        var result = new LeavePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
        var metas = result.GetMetaData<List<LeaveMetaDataModel>>("PaidLeave");
        metas.Should().NotBeNull().And.BeEmpty(); // the list is always set, just never appended to
    }

    [Fact]
    public void GovernmentSourcedApplication_IsAlsoEmpty_IneligibleForVirtualAttendance()
    {
        var context = BuildContext();
        var leave = BuildLeaveApplication(DurationType.Partial, paySource: PaySource.Government,
            startTime: new DateTime(2026, 1, 5, 10, 0, 0), endTime: new DateTime(2026, 1, 5, 12, 0, 0));
        ApplyLeave(context, leave);

        var result = new LeavePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void MultipleEligibleApplications_CombinesRangesAndBuildsOneMetaEntryEach()
    {
        var context = BuildContext();
        var morning = BuildLeaveApplication(DurationType.Partial,
            startTime: new DateTime(2026, 1, 5, 9, 0, 0), endTime: new DateTime(2026, 1, 5, 10, 0, 0));
        var afternoon = BuildLeaveApplication(DurationType.Partial,
            startTime: new DateTime(2026, 1, 5, 14, 0, 0), endTime: new DateTime(2026, 1, 5, 15, 0, 0));
        ApplyLeave(context, morning, afternoon);

        var result = new LeavePolicy(new AlwaysTrueSpec()).Apply(TimeRange.Empty, context);

        result.TotalMinutes.Should().Be(120); // 60 + 60
        var metas = result.GetMetaData<List<LeaveMetaDataModel>>("PaidLeave");
        metas.Should().HaveCount(2);
    }
}
