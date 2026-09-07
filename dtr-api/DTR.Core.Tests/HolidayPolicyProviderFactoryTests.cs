using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests;

/// <summary>
/// HolidayPolicyProviderFactory's two providers — the code that actually computes the
/// holiday/non-holiday minute split for a day and records it on the ledger
/// (holiday_portion_{type}, non_holiday_portion). RegularDayEvaluator/RestDayEvaluator (see
/// EvaluatorsTests) trust this split instead of re-deriving it from the coarse
/// IsLegalHoliday() boolean — this is the layer that must be correct for that fix to hold.
/// </summary>
public class HolidayPolicyProviderFactoryTests : DtrTestBase
{
    private static readonly DateOnly HolidayDate = new(2026, 1, 2);
    private static readonly DateTime ShiftStart = new(2026, 1, 1, 22, 0, 0);
    private static readonly DateTime ShiftEnd = new(2026, 1, 2, 6, 0, 0); // crosses midnight into the holiday

    private static TimeContext BuildCrossingShiftContext(HolidayTimeBasis basis)
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(ShiftStart, ShiftEnd, basis, holidays, employeeId);
        // Anchor the DTR row to the holiday date, matching how a graveyard shift crossing
        // into a holiday is normally logged — see EvaluatorsTests for the same convention.
        context.Payload.Data.CurrentShift.ShiftDate = HolidayDate;
        context.Payload.Data.CurrentDate = HolidayDate;
        return context;
    }

    [Fact]
    public void TimeInDayTypeProvider_TreatsTheWholeShiftAsHoliday_NoRegularRemainder()
    {
        var context = BuildCrossingShiftContext(HolidayTimeBasis.BasedOnTimeInDayType);
        var regularRange = context.CanonicalTimeRange; // whole 8-hour shift
        var provider = new TimeInDayTypeProvider(regularRange, context);

        var result = provider.Calculate(HolidayType.LEGAL, regularRange);

        result.Should().Be(regularRange);
        context.Payload.Ledger.GetByTag("holiday_portion_LEGAL", context).TotalMinutes.Should().Be(480);
        context.Payload.Ledger.GetByTag("non_holiday_portion", context).IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void ActualWorkHoursProvider_SplitsTheShift_HolidayPortionOnlyCoversTheOverlap()
    {
        var context = BuildCrossingShiftContext(HolidayTimeBasis.BasedOnActualWorkHours);
        var regularRange = context.CanonicalTimeRange; // Jan1 22:00 - Jan2 06:00
        var provider = new ActualWorkHoursProvider(regularRange, context);

        provider.Calculate(HolidayType.LEGAL, regularRange);

        // Holiday portion: Jan2 00:00 - 06:00 = 6 hours (the part actually inside the holiday).
        context.Payload.Ledger.GetByTag("holiday_portion_LEGAL", context).TotalMinutes.Should().Be(360);
        // Non-holiday remainder: Jan1 22:00 - Jan2 00:00 = 2 hours.
        context.Payload.Ledger.GetByTag("non_holiday_portion", context).TotalMinutes.Should().Be(120);
    }

    [Fact]
    public void ActualWorkHoursProvider_ShiftEntirelyOutsideTheHoliday_ReturnsEmpty()
    {
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        // A plain daytime shift on Jan 1 — never touches the Jan 2 holiday at all.
        var context = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        var regularRange = context.CanonicalTimeRange;
        var provider = new ActualWorkHoursProvider(regularRange, context);

        var result = provider.Calculate(HolidayType.LEGAL, regularRange);

        result.IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void ActualWorkHoursProvider_ShiftEntirelyInsideTheHoliday_NoRegularRemainder()
    {
        // A plain daytime shift entirely on the holiday itself — nothing crosses a boundary.
        var employeeId = NewEmployeeId();
        var holidays = Holidays(employeeId, Holiday(HolidayType.LEGAL, HolidayDate));
        var context = CreateContext(
            new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 17, 0, 0),
            HolidayTimeBasis.BasedOnActualWorkHours, holidays, employeeId);
        var regularRange = context.CanonicalTimeRange;
        var provider = new ActualWorkHoursProvider(regularRange, context);

        provider.Calculate(HolidayType.LEGAL, regularRange);

        context.Payload.Ledger.GetByTag("holiday_portion_LEGAL", context).TotalMinutes.Should().Be(540);
        context.Payload.Ledger.GetByTag("non_holiday_portion", context).IsEmpty().Should().BeTrue();
    }
}
