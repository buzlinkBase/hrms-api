using Hrms.Core.Services;
using Hrms.Domain.Entities;

namespace hrms.test.SetupTests;

/// <summary>
/// HolidayService.ProjectHolidaysForRange — the exact per-row date-resolution + range-filter
/// logic behind GetAllHolidays, which is what the DTR pipeline (dtr-api's
/// HolidayQueryService.GetHolidayInfoAsync) calls on every real payroll/DTR run. Pulled out as
/// internal static so this is testable without a database — verifies the wiring
/// HolidayRecurrenceCalculatorTests doesn't reach (that file only tests the pure date math in
/// isolation, not that GetAllHolidays actually applies it correctly per-row and per-range).
/// </summary>
public class HolidayServiceProjectHolidaysForRangeTests
{
    private static Holiday NthWeekdayHoliday(int month, DayOfWeek dayOfWeek, int weekOfMonth, string description = "NthWeekday") => new()
    {
        Description = description,
        HolType = HolidayType.LEGAL,
        WorkType = HolidayWorkType.NonWorking,
        HolDate = new DateOnly(2026, month, 1), // seed day is irrelevant once WeekOfMonth/DayOfWeek are set — only Month matters
        HolYear = 2026,
        IsRecuring = true,
        WeekOfMonth = weekOfMonth,
        DayOfWeek = dayOfWeek,
        IsPaid = true,
    };

    private static Holiday FixedRecurringHoliday(int month, int day, string description = "FixedRecurring") => new()
    {
        Description = description,
        HolType = HolidayType.LEGAL,
        WorkType = HolidayWorkType.NonWorking,
        HolDate = new DateOnly(2020, month, day),
        HolYear = 2020,
        IsRecuring = true,
        IsPaid = true,
    };

    private static Holiday NonRecurringHoliday(DateOnly date, string description = "OneOff") => new()
    {
        Description = description,
        HolType = HolidayType.SPECIAL,
        WorkType = HolidayWorkType.NonWorking,
        HolDate = date,
        HolYear = date.Year,
        IsRecuring = false,
        IsPaid = false,
    };

    [Fact]
    public void NthWeekdayHoliday_ResolvesToCorrectDate_WithinASingleMonthDtrRange()
    {
        // "Last Monday of August" requested for a plain August 2026 DTR range — Aug 31,
        // 2026 is itself a Monday (matches HolidayRecurrenceCalculatorTests).
        var holidays = new List<Holiday> { NthWeekdayHoliday(8, DayOfWeek.Monday, 5) };

        var result = HolidayService.ProjectHolidaysForRange(holidays, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        result.Should().ContainSingle();
        result[0].HolDate.Should().Be(new DateOnly(2026, 8, 31));
    }

    [Fact]
    public void NthWeekdayHoliday_OutsideTheRequestedCutoff_IsExcluded()
    {
        // A Semi-Monthly first cutoff (Aug 1-15) requested while the holiday's actual
        // resolved date (Aug 31, last Monday) falls in the second cutoff — must NOT
        // falsely appear in the first cutoff's DTR run.
        var holidays = new List<Holiday> { NthWeekdayHoliday(8, DayOfWeek.Monday, 5) };

        var result = HolidayService.ProjectHolidaysForRange(holidays, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 15));

        result.Should().BeEmpty();
    }

    [Fact]
    public void NthWeekdayHoliday_CrossYearDtrCutoff_EachHolidayResolvesToTheYearItsMonthFallsIn()
    {
        // A Dec 16, 2025 - Jan 15, 2026 cutoff (crossing both month and year) — a December
        // holiday resolves against from.Year (2025), a January holiday against to.Year
        // (2026), exactly like the existing fixed-recurring cross-year logic.
        var decemberHoliday = NthWeekdayHoliday(12, DayOfWeek.Monday, 5, "Last Monday of December"); // -> Dec 29, 2025
        var januaryHoliday = NthWeekdayHoliday(1, DayOfWeek.Monday, 1, "First Monday of January"); // -> Jan 5, 2026
        var holidays = new List<Holiday> { decemberHoliday, januaryHoliday };

        var result = HolidayService.ProjectHolidaysForRange(holidays, new DateOnly(2025, 12, 16), new DateOnly(2026, 1, 15));

        result.Should().HaveCount(2);
        result.Should().ContainSingle(h => h.Description == "Last Monday of December" && h.HolDate == new DateOnly(2025, 12, 29));
        result.Should().ContainSingle(h => h.Description == "First Monday of January" && h.HolDate == new DateOnly(2026, 1, 5));
    }

    [Fact]
    public void FixedRecurringHoliday_StillResolvesByMonthAndDay_UnaffectedByNthWeekdaySupport()
    {
        var holidays = new List<Holiday> { FixedRecurringHoliday(1, 1, "New Year's Day") };

        var result = HolidayService.ProjectHolidaysForRange(holidays, new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31));

        result.Should().ContainSingle();
        result[0].HolDate.Should().Be(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void NonRecurringHoliday_PassesThroughWithItsExactStoredDate()
    {
        var holidays = new List<Holiday> { NonRecurringHoliday(new DateOnly(2026, 2, 17), "Chinese New Year") };

        var result = HolidayService.ProjectHolidaysForRange(holidays, new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28));

        result.Should().ContainSingle();
        result[0].HolDate.Should().Be(new DateOnly(2026, 2, 17));
        result[0].IsRecuring.Should().BeFalse();
    }
}
