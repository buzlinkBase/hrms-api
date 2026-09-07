using DTR.Core.Tests.TestSupport;
using Hrms.Domain.Entities;

namespace DTR.Core.Tests;

/// <summary>
/// WorkTypeResolver.Resolve — the documented 9-step priority order (see the class's own doc
/// comment): incomplete attendance beats everything; then duty (has attendance) vs. no-duty
/// branches, each ranking rest-day/holiday above leave, and leave above the plain fallbacks.
/// CurrentLeaves here is the plain DataPayload list WorkTypeResolver reads directly (PayType
/// only) — it never touches the Leave strategy/pipeline subsystem that's out of scope for this
/// session, so building LeaveApplication objects inline for these tests doesn't duplicate that
/// work.
/// </summary>
public class WorkTypeResolverTests : DtrTestBase
{
    private static readonly DateTime ShiftStart = new(2026, 1, 5, 8, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 5, 17, 0, 0);
    private static readonly DateOnly Day = new(2026, 1, 5);
    private static readonly IWorkTypeResolver Resolver = new WorkTypeResolver();

    private static LeaveApplication LeaveOf(PayType payType, PaySource paySource = PaySource.Company, PayoutMode payoutMode = PayoutMode.PerDay) =>
        new LeaveApplication
        {
            Leave = new Leave { PaySource = paySource },
            PayType = payType,
            PayoutMode = payoutMode,
            DurationType = DurationType.SingleDay,
            LeaveDateFrom = Day,
            LeaveDateTo = Day,
        };

    // --- 1. Incomplete attendance always wins -----------------------------------------------

    [Fact]
    public void OddPunchCount_ReturnsIncomplete_EvenOnALegalHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        ApplyAttendance(context, ShiftStart); // single, unpaired punch

        Resolver.Resolve(context).Should().Be(WorkType.Incomplete);
    }

    // --- GovFundedLeave outranks everything else after Incomplete ---------------------------

    [Fact]
    public void GovFundedOneTimeLeave_ReturnsGovFundedLeave()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication>
        {
            LeaveOf(PayType.WithPay, PaySource.Government, PayoutMode.OneTime),
        };

        Resolver.Resolve(context).Should().Be(WorkType.GovFundedLeave);
    }

    // --- 2/4. Has attendance, no rest/holiday/leave -> plain regular duty -------------------

    [Fact]
    public void HasAttendance_PlainDay_ReturnsRegularWorkDay()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.RegularWorkDay);
    }

    [Fact]
    public void HasAttendance_RestDay_ReturnsRestDayDuty()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        MarkAsRestDay(context);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.RestDayDuty);
    }

    [Fact]
    public void HasAttendance_LegalHoliday_ReturnsLegalHolidayDuty()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.LegalHolidayDuty);
    }

    [Fact]
    public void HasAttendance_SpecialNonWorkingHoliday_ReturnsSpecialHolidayDuty()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, Day, HolidayWorkType.NonWorking));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.SpecialHolidayDuty);
    }

    [Fact]
    public void HasAttendance_SpecialWorkingHoliday_ReturnsSpecialWorkingHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, Day, HolidayWorkType.Working));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.SpecialWorkingHoliday);
    }

    [Fact]
    public void HasAttendance_RestDayBeatsLegalHoliday_ReturnsRestDayLegalHolidayDuty()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        MarkAsRestDay(context);
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.RestDayLegalHolidayDuty);
    }

    // --- 3. Duty + leave, no rest/holiday -> paid/unpaid leave (leave outranks plain duty) --

    [Fact]
    public void HasAttendance_PlainDayWithUnpaidLeave_ReturnsUnpaidLeave()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication> { LeaveOf(PayType.WithoutPay) };
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.UnpaidLeave);
    }

    [Fact]
    public void HasAttendance_PlainDayWithMixedLeave_PaidOutranksUnpaid()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication>
        {
            LeaveOf(PayType.WithoutPay),
            LeaveOf(PayType.WithPay),
        };
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.PaidLeave);
    }

    [Fact]
    public void HasAttendance_RestDayWithLeave_RestDayStillWins()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        MarkAsRestDay(context);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication> { LeaveOf(PayType.WithPay) };
        ApplyAttendance(context, ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.RestDayDuty);
    }

    // --- 5. No attendance, rest day (+ holiday combinations) ---------------------------------

    [Fact]
    public void NoAttendance_PlainRestDay_ReturnsRestDay()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        MarkAsRestDay(context);

        Resolver.Resolve(context).Should().Be(WorkType.RestDay);
    }

    [Fact]
    public void NoAttendance_RestDayPlusLegalHoliday_ReturnsRestDayLegalHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        MarkAsRestDay(context);

        Resolver.Resolve(context).Should().Be(WorkType.RestDayLegalHoliday);
    }

    [Fact]
    public void NoAttendance_RestDayPlusSpecialNonWorking_ReturnsRestDaySpecialHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, Day, HolidayWorkType.NonWorking));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        MarkAsRestDay(context);

        Resolver.Resolve(context).Should().Be(WorkType.RestDaySpecialHoliday);
    }

    // --- 6/7. No attendance, no rest day: legal / special non-working holiday ---------------

    [Fact]
    public void NoAttendance_LegalHoliday_ReturnsLegalHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);

        Resolver.Resolve(context).Should().Be(WorkType.LegalHoliday);
    }

    [Fact]
    public void NoAttendance_SpecialNonWorkingHoliday_ReturnsSpecialNonWorkingHoliday()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, Day, HolidayWorkType.NonWorking));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);

        Resolver.Resolve(context).Should().Be(WorkType.SpecialNonWorkingHoliday);
    }

    // --- 8. No attendance, plain day, on leave -----------------------------------------------

    [Fact]
    public void NoAttendance_PlainDayWithPaidLeave_ReturnsPaidLeave()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication> { LeaveOf(PayType.WithPay) };

        Resolver.Resolve(context).Should().Be(WorkType.PaidLeave);
    }

    [Fact]
    public void NoAttendance_PlainDayWithUnpaidLeave_ReturnsUnpaidLeave()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication> { LeaveOf(PayType.WithoutPay) };

        Resolver.Resolve(context).Should().Be(WorkType.UnpaidLeave);
    }

    [Fact]
    public void NoAttendance_LegalHolidayBeatsLeave()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, Day));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);
        context.Payload.Data.CurrentLeaves = new List<LeaveApplication> { LeaveOf(PayType.WithPay) };

        Resolver.Resolve(context).Should().Be(WorkType.LegalHoliday);
    }

    // --- 9. Fallbacks: special working (no attendance) / travel / plain absent --------------

    [Fact]
    public void NoAttendance_SpecialWorkingHoliday_ReturnsAbsent()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, Day, HolidayWorkType.Working));
        var context = CreateContext(ShiftStart, ShiftEnd, holidays: holidays, employeeId: employeeId);

        Resolver.Resolve(context).Should().Be(WorkType.Absent);
    }

    [Fact]
    public void NoAttendance_PlainDayWithTravel_ReturnsTravel()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);
        context.Payload.Data.CurrentTravel = new TravelOrderApplication
        {
            StartDate = Day,
            EndDate = Day,
        };

        Resolver.Resolve(context).Should().Be(WorkType.Travel);
    }

    [Fact]
    public void NoAttendance_RestDayWithTravel_ReturnsRestDay_TravelNeverConsulted()
    {
        // Resolve's own rest-day branch returns WorkType.RestDay unconditionally once
        // isRestDay is true, before ever reaching the `if (travel != null)` check further
        // down — so ResolveTravelWorkType's RestDayTravel outcome is unreachable through
        // Resolve's actual control flow today, regardless of what CurrentTravel holds.
        var context = CreateContext(ShiftStart, ShiftEnd);
        MarkAsRestDay(context);
        context.Payload.Data.CurrentTravel = new TravelOrderApplication
        {
            StartDate = Day,
            EndDate = Day,
        };

        Resolver.Resolve(context).Should().Be(WorkType.RestDay);
    }

    [Fact]
    public void NoAttendance_PlainDayNoLeaveNoTravel_ReturnsAbsent()
    {
        var context = CreateContext(ShiftStart, ShiftEnd);

        Resolver.Resolve(context).Should().Be(WorkType.Absent);
    }
}
