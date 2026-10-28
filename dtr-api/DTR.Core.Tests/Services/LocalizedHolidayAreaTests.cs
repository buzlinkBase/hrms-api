using Hrms.Domain.Entities;

namespace DTR.Core.Tests.Services;

/// <summary>
/// A localized special holiday can cover several project sites. It applies when the site the
/// day is recorded at (DTRLocationResolver: rotation-day site, then first punch, then the
/// employee's own) is one of them; a special holiday with no sites is nationwide.
/// </summary>
public class LocalizedHolidayAreaTests
{
    private static readonly DateOnly Day = new(2026, 9, 9);
    // Fixed ids: theory data is built at discovery, so NewGuid() would differ at run time.
    private static readonly Guid Cebu = new("00000000-0000-0000-0000-00000000ceb0");
    private static readonly Guid Mandaue = new("00000000-0000-0000-0000-0000000a0de0");
    private static readonly Guid Manila = new("00000000-0000-0000-0000-00000000a1a0");

    private static HolidayInfo Special(params Guid[] areaIds) => new()
    {
        HolidayId = Guid.NewGuid(),
        HolType = HolidayType.SPECIAL,
        WorkType = HolidayWorkType.NonWorking,
        PayrollDate = Day,
        AreaIds = areaIds,
    };

    // --- HolidayFilterChain ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SitesOnTheHoliday))]
    public void LocalizedSpecial_AppliesOnlyAtItsSites(Guid dayArea, bool expected)
    {
        var holiday = Special(Cebu, Mandaue);

        new HolidayFilterChain().IsApplicable(holiday, dayArea).Should().Be(expected);
    }

    public static TheoryData<Guid, bool> SitesOnTheHoliday() => new()
    {
        { Cebu, true },
        { Mandaue, true },
        { Manila, false },
    };

    [Fact]
    public void LocalizedSpecial_DayWithNoSite_DoesNotApply()
    {
        new HolidayFilterChain().IsApplicable(Special(Cebu, Mandaue), null).Should().BeFalse();
    }

    [Fact]
    public void NationwideSpecial_AppliesEverywhere()
    {
        var chain = new HolidayFilterChain();

        chain.IsApplicable(Special(), Manila).Should().BeTrue();
        chain.IsApplicable(Special(), null).Should().BeTrue();
    }

    [Fact]
    public void Legal_AppliesEverywhere()
    {
        var legal = new HolidayInfo { HolidayId = Guid.NewGuid(), HolType = HolidayType.LEGAL, PayrollDate = Day };

        new HolidayFilterChain().IsApplicable(legal, Manila).Should().BeTrue();
        new HolidayFilterChain().IsApplicable(legal, null).Should().BeTrue();
    }

    // --- CurrentDayDTRPayload: which site the holiday provider checks -------------------------

    private static TimeRecordCollection SpecialHoursOnTheDay(EmployeeDTRRun employee, Guid? scheduledArea, HolidayInfo holiday)
    {
        CurrentShift ShiftOn(DateOnly date) => new()
        {
            StartTime = date.ToDateTime(new TimeOnly(8, 0)),
            EndTime = date.ToDateTime(new TimeOnly(17, 0)),
            ShiftDate = date,
            ShiftType = TimeShiftType.FIXED,
            MaxWorkingMinutes = 540,
            AreaId = date == Day ? scheduledArea : null,
        };
        holiday.EmployeeId = employee.Id;

        var context = new DTRContextBuilder()
            .WithEmployees([employee])
            .WithShifts(new Dictionary<CurrentTimeShiftKey, CurrentShift>
            {
                [new CurrentTimeShiftKey(employee.Id, Day)] = ShiftOn(Day),
                [new CurrentTimeShiftKey(employee.Id, Day.AddDays(1))] = ShiftOn(Day.AddDays(1)),
            })
            .WithCleanAttendance(new Dictionary<AttendanceEmpId, List<Attendance>>())
            .WithOverTimeApplications(new Dictionary<OTKey, OverTimeApplication?>())
            .WithUnderTimeApplications(new Dictionary<UTKey, UnderTimeApplication?>())
            .WithLeaves(new Dictionary<Leavekey, List<LeaveApplication>>())
            .WithTravels(new Dictionary<TravelKey, List<TravelOrderApplication>>())
            .WithHolidays(new Dictionary<Holidaykey, List<HolidayInfo>>
            {
                [new Holidaykey(employee.Id, Day)] = [holiday],
            })
            .WithDayOffs(new Dictionary<ResDaykey, CurrentRestDay>())
            .WithCompanyPolicy(null)
            .WithClientPolicy(new Dictionary<ClientPolicyKey, ClientPolicyRule>())
            .WithEmployeePolicy(new Dictionary<EmployeePolicyKey, EmployeePolicyRule>())
            .Build();

        var payload = CurrentDayDTRPayload.SetPayload(context, employee, Day);
        return payload.Provider.HolidayProvider.GetHolidayDuringDate(HolidayType.SPECIAL, employee, Day);
    }

    [Fact]
    public void RelieverScheduledAtACebuSite_GetsTheCebuHoliday_EvenThoughHomeIsManila()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid(), AreaId = Manila };

        SpecialHoursOnTheDay(employee, scheduledArea: Mandaue, Special(Cebu, Mandaue)).Should().NotBeEmpty();
    }

    [Fact]
    public void CebuEmployeeScheduledInManila_DoesNotGetTheCebuHoliday()
    {
        var employee = new EmployeeDTRRun { Id = Guid.NewGuid(), AreaId = Cebu };

        SpecialHoursOnTheDay(employee, scheduledArea: Manila, Special(Cebu, Mandaue)).Should().BeEmpty();
    }

    [Fact]
    public void NoScheduledSite_FallsBackToTheEmployeesOwnSite()
    {
        var cebuEmployee = new EmployeeDTRRun { Id = Guid.NewGuid(), AreaId = Cebu };
        var manilaEmployee = new EmployeeDTRRun { Id = Guid.NewGuid(), AreaId = Manila };

        SpecialHoursOnTheDay(cebuEmployee, scheduledArea: null, Special(Cebu, Mandaue)).Should().NotBeEmpty();
        SpecialHoursOnTheDay(manilaEmployee, scheduledArea: null, Special(Cebu, Mandaue)).Should().BeEmpty();
    }
}
