namespace DTR.Core;

public static class DTRPipelineKeys
{
    public const string Regular = "regular";
    public const string Travel = "travel";
    public const string Leave = "leave";
    public const string Plus8 = "plus8";
    public const string OT = "ot";
    public const string Late = "late";
    public const string UT = "ut";
    public const string Overbreak = "overbreak";
}

public interface IDTRTimePipeline
{
    TimeRange Apply(TimeContext context, TimeRange input);
}

public interface IHolidayDutyTimePipeline
{
    TimeRange Apply(TimeContext context, HolidayType holidayType, TimeRange input);
}
