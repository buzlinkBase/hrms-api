using DTR.Core.DTR.DisplayRule.ColumnsViewRule.DisplayRules;
using DTR.Core.Tests.TestSupport;

namespace DTR.Core.Tests.DisplayRules;

/// <summary>
/// HolidayPlusRegularRule/LegalHolidayRule — confirms the "range" RegularDayEvaluator/
/// LegalHolidayEvaluator actually receive in production really is pipeline.Regular/
/// pipeline.LegalHoliday in the normal case (IsHolPlusReg = false, not Special-Working),
/// which is what RegularDayEvaluatorTests relies on.
/// </summary>
public class HolidayPlusRegularRuleTests : DtrTestBase
{
    private static DisplayContext BuildContext(bool isHolPlusReg, TimeRange regular, TimeRange legalHoliday)
    {
        var timeContext = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType);
        timeContext.Payload.Data.CompanyPolicy.IsHolPlusReg = isHolPlusReg;
        return CreateDisplayContext(timeContext, new PipeLineResult { Regular = regular, LegalHoliday = legalHoliday });
    }

    [Fact]
    public void IsHolPlusRegFalse_NotSpecialWorking_ReturnsPipelineRegularDirectly()
    {
        var regular = Range(new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0));
        var holiday = Range(new DateTime(2026, 1, 1, 17, 0, 0), new DateTime(2026, 1, 1, 19, 0, 0));
        var context = BuildContext(isHolPlusReg: false, regular, holiday);

        var result = new HolidayPlusRegularRule().ApplyRules(context);

        result.Should().Be(regular); // holiday is NOT folded in
    }

    [Fact]
    public void IsHolPlusRegTrue_CombinesRegularAndHoliday()
    {
        var regular = Range(new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 12, 0, 0)); // 4h
        var holiday = Range(new DateTime(2026, 1, 1, 13, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0)); // 4h
        var context = BuildContext(isHolPlusReg: true, regular, holiday);

        var result = new HolidayPlusRegularRule().ApplyRules(context);

        result.TotalMinutes.Should().Be(480);
    }

    [Fact]
    public void IsHolPlusRegFalse_SpecialWorkingDay_ReturnsPipelineSpecialHolidayInstead()
    {
        var employeeId = NewEmployeeId();
        var specialDate = new DateOnly(2026, 1, 2);
        var holidays = Holidays(employeeId, Holiday(HolidayType.SPECIAL, specialDate, HolidayWorkType.Working));
        var timeContext = CreateContext(
            new DateTime(2026, 1, 2, 8, 0, 0), new DateTime(2026, 1, 2, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType, holidays, employeeId);
        timeContext.Payload.Data.CurrentShift.ShiftDate = specialDate;
        timeContext.Payload.Data.CurrentDate = specialDate;
        timeContext.Payload.Data.CompanyPolicy.IsHolPlusReg = false;

        var regular = timeContext.CanonicalTimeRange;
        var specialHoliday = timeContext.CanonicalTimeRange;
        var displayContext = CreateDisplayContext(
            timeContext, new PipeLineResult { Regular = regular, SpecialHoliday = specialHoliday });

        var result = new HolidayPlusRegularRule().ApplyRules(displayContext);

        result.Should().Be(specialHoliday);
    }
}

public class LegalHolidayRuleTests : DtrTestBase
{
    [Fact]
    public void IsHolPlusRegFalse_ReturnsPipelineLegalHolidayDirectly()
    {
        var timeContext = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType);
        timeContext.Payload.Data.CompanyPolicy.IsHolPlusReg = false;
        var holiday = timeContext.CanonicalTimeRange;
        var context = CreateDisplayContext(timeContext, new PipeLineResult { LegalHoliday = holiday });

        var result = new LegalHolidayRule().ApplyRules(context);

        result.Should().Be(holiday);
    }

    [Fact]
    public void IsHolPlusRegTrue_ReturnsEmpty()
    {
        var timeContext = CreateContext(
            new DateTime(2026, 1, 1, 8, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0),
            HolidayTimeBasis.BasedOnTimeInDayType);
        timeContext.Payload.Data.CompanyPolicy.IsHolPlusReg = true;
        var holiday = timeContext.CanonicalTimeRange;
        var context = CreateDisplayContext(timeContext, new PipeLineResult { LegalHoliday = holiday });

        var result = new LegalHolidayRule().ApplyRules(context);

        result.IsEmpty().Should().BeTrue();
    }
}
