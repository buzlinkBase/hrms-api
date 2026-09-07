using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// NonHolidayDutyTimePipeline — decides whether a day's Regular hours come from the plain
/// attendance computation (WorkTimePipeline) or from the ledger's already-basis-split
/// "non_holiday_portion" tag. WorkTimePipeline's own internals (breaks, grace period, late/UT,
/// OT top-up — an entire separate attendance-computation subsystem) are out of scope here; the
/// "work_time" ledger key is pre-seeded to short-circuit that computation so these tests
/// isolate exactly this pipeline's own branching responsibility, matching how
/// HolidayPolicyProviderFactoryTests already covers where "non_holiday_portion" itself comes
/// from.
/// </summary>
public class NonHolidayDutyTimePipelineTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 1, 2);
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 22, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 6, 0, 0);

    [Fact]
    public void Apply_NoHolidayTouch_ReturnsTheComputedWorkRange()
    {
        var context = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType); // no holidays seeded -> IsHolidaySpec never satisfied

        var seededWorkRange = Range(new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 16, 0, 0));
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey("work_time", context), seededWorkRange);

        var result = new NonHolidayDutyTimePipeline().Apply(context, context.CanonicalTimeRange);

        result.Should().Be(seededWorkRange);
    }

    [Fact]
    public void Apply_BasedOnActualWorkHours_BoundaryCrossingDay_ReturnsTheLedgersNonHolidayPortion()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;

        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey("work_time", context), TimeRange.Empty);
        var nonHolidayRemainder = Range(ShiftStart, new DateTime(2026, 1, 2, 0, 0, 0));
        context.Payload.Ledger.RecordByTag("non_holiday_portion", context, nonHolidayRemainder);

        var result = new NonHolidayDutyTimePipeline().Apply(context, context.CanonicalTimeRange);

        result.Should().Be(nonHolidayRemainder);
    }

    [Fact]
    public void Apply_HolidayTouchingDay_NonHolidayPortionNeverRecorded_ReturnsEmpty()
    {
        // Guards the pipeline's own fallback — if HolidayPolicyProviderFactory somehow never
        // ran (e.g. a future refactor breaks the WorkTimePipeline -> HolidayPolicy wiring),
        // this must fail safe to Empty rather than leaking an unrelated value.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey("work_time", context), TimeRange.Empty);

        var result = new NonHolidayDutyTimePipeline().Apply(context, context.CanonicalTimeRange);

        result.IsEmpty().Should().BeTrue();
    }
}
