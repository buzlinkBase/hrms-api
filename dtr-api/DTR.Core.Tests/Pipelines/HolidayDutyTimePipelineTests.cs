using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.Pipelines;

/// <summary>
/// HolidayDutyTimePipeline — the holiday-column counterpart to NonHolidayDutyTimePipeline.
/// Same isolation approach: "work_time" is pre-seeded to short-circuit WorkTimePipeline's own
/// attendance computation (out of scope here), so these tests cover only this pipeline's own
/// responsibility — reading back whichever holiday_portion_{type} HolidayPolicyProviderFactory
/// already recorded.
/// </summary>
public class HolidayDutyTimePipelineTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 1, 2);
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 22, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 6, 0, 0);

    [Fact]
    public void Apply_ReturnsTheLedgersHolidayPortion()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(ShiftStart, ShiftEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;

        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey("work_time", context), TimeRange.Empty);
        var holidayPortion = Range(new DateTime(2026, 1, 2, 0, 0, 0), ShiftEnd); // 6 hours
        context.Payload.Ledger.RecordByTag("holiday_portion_LEGAL", context, holidayPortion);

        var result = new HolidayDutyTimePipeline().Apply(context, HolidayType.LEGAL, context.CanonicalTimeRange);

        result.Should().Be(holidayPortion);
    }

    [Fact]
    public void Apply_NothingRecorded_ReturnsEmpty()
    {
        var context = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType);
        context.Payload.Ledger.Record(TimeRangeLedger.CreateKey("work_time", context), TimeRange.Empty);

        var result = new HolidayDutyTimePipeline().Apply(context, HolidayType.LEGAL, context.CanonicalTimeRange);

        result.IsEmpty().Should().BeTrue();
    }
}
