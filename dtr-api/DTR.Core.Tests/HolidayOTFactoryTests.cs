using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests;

/// <summary>
/// HolidayOTFactory's two providers — the OT analog of HolidayPolicyProviderFactory. Under
/// BasedOnActualWorkHours, ActualDayOTHourProvider records the non-holiday OT remainder under
/// "REGULAR_OT_ACTUAL_{type}" — the ledger key RegularOverTimeEvaluator/RestOverTimeEvaluator
/// read to restore a boundary-crossing shift's non-holiday OT instead of discarding it.
/// </summary>
public class HolidayOTFactoryTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 1, 2);

    [Fact]
    public void TimeInDayOTHourProvider_ReturnsTheWholeOTRange_NeverRecordsARemainder()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var otStart = new DateTime(2026, 1, 1, 22, 0, 0);
        var otEnd = new DateTime(2026, 1, 2, 2, 0, 0);
        var context = CreateContext(otStart, otEnd, HolidayTimeBasis.BasedOnTimeInDayType, holidays, employeeId);
        var ot = Range(otStart, otEnd); // 4 hours

        var provider = new TimeInDayOTHourProvider(context, ot);
        var result = provider.Calculate(HolidayType.LEGAL);

        result.Should().Be(ot);
        context.Payload.Ledger.GetByTag("REGULAR_OT_ACTUAL_LEGAL", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void ActualDayOTHourProvider_SplitsOTAcrossTheHolidayBoundary()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var otStart = new DateTime(2026, 1, 1, 22, 0, 0);
        var otEnd = new DateTime(2026, 1, 2, 2, 0, 0); // 4 hours OT: 2 before midnight, 2 after
        var context = CreateContext(otStart, otEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        var ot = Range(otStart, otEnd);

        var provider = new ActualDayOTHourProvider(context, ot);
        var result = provider.Calculate(HolidayType.LEGAL);

        result.TotalMinutes.Should().Be(120); // holiday-overlapping OT: Jan2 00:00-02:00
        context.Payload.Ledger.GetByTag("REGULAR_OT_ACTUAL_LEGAL", context).TotalMinutes.Should().Be(120); // non-holiday OT: Jan1 22:00-00:00
    }

    [Fact]
    public void ActualDayOTHourProvider_OTEntirelyOutsideTheHoliday_NoRemainderRecorded()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var otStart = new DateTime(2026, 1, 1, 17, 0, 0);
        var otEnd = new DateTime(2026, 1, 1, 19, 0, 0);
        var context = CreateContext(otStart, otEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        var ot = Range(otStart, otEnd);

        var provider = new ActualDayOTHourProvider(context, ot);
        var result = provider.Calculate(HolidayType.LEGAL);

        result.IsEmpty().Should().BeTrue();
        context.Payload.Ledger.GetByTag("REGULAR_OT_ACTUAL_LEGAL", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void ActualDayOTHourProvider_OTEntirelyInsideTheHoliday_NoRemainderRecorded()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var otStart = new DateTime(2026, 1, 2, 18, 0, 0);
        var otEnd = new DateTime(2026, 1, 2, 20, 0, 0);
        var context = CreateContext(otStart, otEnd, HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        var ot = Range(otStart, otEnd);

        var provider = new ActualDayOTHourProvider(context, ot);
        var result = provider.Calculate(HolidayType.LEGAL);

        result.TotalMinutes.Should().Be(120);
        context.Payload.Ledger.GetByTag("REGULAR_OT_ACTUAL_LEGAL", context).IsEmpty().Should().BeTrue();
    }
}
