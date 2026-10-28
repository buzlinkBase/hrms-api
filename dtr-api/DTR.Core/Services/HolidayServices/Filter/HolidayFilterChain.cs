namespace DTR.Core;

public class HolidayFilterChain
{
    private readonly List<IHolidayFilterStrategy> _strategies;

    public HolidayFilterChain()
    {
        _strategies = new List<IHolidayFilterStrategy>
        {
            new RegularHolidayStrategy(),
            new SpecialAreaHolidayStrategy(),
            new SpecialNationalHolidayStrategy(),
        };
    }

    // dayAreaId: the project site the employee's day is recorded at (DTRLocationResolver).
    public bool IsApplicable(HolidayInfo holiday, Guid? dayAreaId)
        => _strategies.Any(strategy => strategy.IsApplicable(holiday, dayAreaId));
}