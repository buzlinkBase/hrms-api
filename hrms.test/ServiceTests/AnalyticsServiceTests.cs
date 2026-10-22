using Hrms.Core.Services.Analytics;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;

namespace hrms.test.ServiceTests;

/// <summary>Shared seed for the Analytics tests: two departments, and a caller who can't see confidential employees.</summary>
internal static class AnalyticsSeed
{
    public static readonly Guid Sales = Guid.NewGuid();
    public static readonly Guid Ops = Guid.NewGuid();
    public static readonly Guid PayrollGroup = Guid.NewGuid();

    public static AnalyticsFilter Range(DateOnly from, DateOnly to, Guid? departmentId = null) =>
        new() { From = from, To = to, DepartmentId = departmentId };

    public static Employee Employee(Guid departmentId, string name, DateOnly hired, DateTime? resigned = null, bool confidential = false) => new()
    {
        Id = Guid.NewGuid(), FirstName = name, LastName = "Test", JobLevel = "", BioId = Random.Shared.Next(1, int.MaxValue),
        DepartmentId = departmentId, PayrollGroupId = PayrollGroup, HireDate = hired, DateResigned = resigned, IsConfidential = confidential,
        EmploymentStatus = EmploymentStatus.Regular, Gender = "Female",
    };

    public static async Task SeedAsync(SqliteHrmsContext db, Action<HrmsContext> add)
    {
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = PayrollGroup, Name = "Main" });
        seed.Departments.AddRange(
            new Department { Id = Sales, Code = "S", Name = "Sales" },
            new Department { Id = Ops, Code = "O", Name = "Operations" });
        add(seed);
        await seed.SaveChangesAsync();
    }

    /// <summary>A caller without confidential clearance: the given employees are hidden in every class.</summary>
    public sealed class Uncleared(params Guid[] hidden) : IConfidentialAccess
    {
        public bool Restricted => true;
        public Guid[] HiddenEmployeeIdsFor(ConfidentialDataClass dataClass) => hidden;
        public Guid[] ApprovalVisibleRecordIds => [];
        public Guid[] ApprovalVisibleEmployeeIds => [];
    }

    /// <summary>Runs one request in its own scope, closed before the next -- a unit of work holds a write lock while open.</summary>
    public static async Task<AnalyticsResult> RunAsync(
        SqliteHrmsContext db, Func<UnitOfWorkService, Task<AnalyticsResult>> run, IConfidentialAccess? access = null)
    {
        await using var context = access == null ? db.NewContext() : db.NewContext(access);
        return await run(new UnitOfWorkService(context));
    }

    public static decimal Kpi(this AnalyticsResult result, string key) => result.Kpis.Single(k => k.Key == key).Value;

    public static decimal? PreviousKpi(this AnalyticsResult result, string key) => result.Kpis.Single(k => k.Key == key).Previous;

    public static AnalyticsBreakdown Breakdown(this AnalyticsResult result, string key) => result.Breakdowns.Single(b => b.Key == key);
}

/// <summary>Date handling every Analytics area shares: ranges, previous period, months, rounding.</summary>
public class AnalyticsPeriodTests
{
    [Fact]
    public void Normalize_SwapsABackwardsRange_AndCapsItAt24Months()
    {
        var swapped = AnalyticsPeriod.Normalize(AnalyticsSeed.Range(new DateOnly(2026, 3, 31), new DateOnly(2026, 1, 1)));
        swapped.From.Should().Be(new DateOnly(2026, 1, 1));
        swapped.To.Should().Be(new DateOnly(2026, 3, 31));

        var capped = AnalyticsPeriod.Normalize(AnalyticsSeed.Range(new DateOnly(2020, 1, 1), new DateOnly(2026, 6, 30)));
        capped.To.Should().Be(new DateOnly(2026, 6, 30));
        capped.From.Should().Be(new DateOnly(2024, 7, 1));
    }

    [Fact]
    public void Previous_IsTheSameLength_EndingTheDayBefore()
    {
        var previous = AnalyticsPeriod.Previous(AnalyticsSeed.Range(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));

        previous.To.Should().Be(new DateOnly(2026, 3, 31));
        previous.From.Should().Be(new DateOnly(2026, 3, 2));
    }

    [Fact]
    public void Months_ClipTheFirstAndLastMonthToTheRange()
    {
        var months = AnalyticsPeriod.Months(new DateOnly(2026, 1, 15), new DateOnly(2026, 3, 10)).ToList();

        months.Select(m => m.Label).Should().Equal("2026-01", "2026-02", "2026-03");
        months[0].Start.Should().Be(new DateOnly(2026, 1, 15));
        months[1].End.Should().Be(new DateOnly(2026, 2, 28));
        months[2].End.Should().Be(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void Percent_IsZeroForNothing_AndRoundedToOneDecimal()
    {
        AnalyticsPeriod.Percent(1, 0).Should().Be(0);
        AnalyticsPeriod.Percent(1, 3).Should().Be(33.3m);
    }
}

public class WorkforceAnalyticsServiceTests
{
    [Fact]
    public async Task CountsHeadcountHiresSeparationsAndTurnover_PerMonthAndDepartment()
    {
        using var db = new SqliteHrmsContext();
        await AnalyticsSeed.SeedAsync(db, seed => seed.Employees.AddRange(
            AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ana", new DateOnly(2020, 1, 1)),
            AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ben", new DateOnly(2026, 2, 10)),
            AnalyticsSeed.Employee(AnalyticsSeed.Ops, "Cy", new DateOnly(2021, 5, 1), resigned: new DateTime(2026, 3, 15)),
            AnalyticsSeed.Employee(AnalyticsSeed.Ops, "Di", new DateOnly(2026, 7, 1))));

        var result = await AnalyticsSeed.RunAsync(db, uow => new WorkforceAnalyticsService(uow).GetAsync(AnalyticsSeed.Range(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)), default));

        result.Kpi("headcount").Should().Be(2, "Ana and Ben; Cy left in March, Di isn't hired yet");
        result.Kpi("hires").Should().Be(1);
        result.Kpi("separations").Should().Be(1);
        // Month-end headcounts 2, 3, 2 -> average 2.33; 1 separation -> 42.9%.
        result.Kpi("turnover").Should().Be(42.9m);
        result.Series.Select(p => p.Values["headcount"]).Should().Equal(2, 3, 2);
        result.Series.Select(p => p.Values["hires"]).Should().Equal(0, 1, 0);
        result.Breakdown("department").Rows.Should().ContainSingle(r => r.Label == "Sales" && r.Values["count"] == 2);
    }

    [Fact]
    public async Task BreakdownRows_CarryTheKeyThePageDrillsInto()
    {
        using var db = new SqliteHrmsContext();
        await AnalyticsSeed.SeedAsync(db, seed => seed.Employees.AddRange(
            AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ana", new DateOnly(2020, 1, 1)),
            AnalyticsSeed.Employee(AnalyticsSeed.Ops, "Cy", new DateOnly(2025, 6, 1))));

        var result = await AnalyticsSeed.RunAsync(db, uow => new WorkforceAnalyticsService(uow).GetAsync(AnalyticsSeed.Range(new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)), default));

        result.Breakdown("department").Rows.Single(r => r.Label == "Sales").Key.Should().Be(AnalyticsSeed.Sales.ToString());
        result.Breakdown("branch").Rows.Should().ContainSingle().Which.Key.Should().BeNull("nobody has a branch, so there's nothing to filter by");
        result.Breakdown("status").Rows.Should().ContainSingle().Which.Key.Should().Be("Regular");
        result.Breakdown("gender").Rows.Should().ContainSingle().Which.Key.Should().Be("Female");
        result.Breakdown("tenure").Rows.Select(r => r.Key).Should().Equal("0-1", "1-3", "3-5", "5-10", "10-");
        result.Breakdown("age").Rows.Should().OnlyContain(r => r.Key == null, "the employee list can't filter by birth date");
    }

    [Fact]
    public async Task DepartmentFilter_AndConfidentialEmployees_NarrowTheCount()
    {
        using var db = new SqliteHrmsContext();
        var secret = AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Sam", new DateOnly(2020, 1, 1), confidential: true);
        await AnalyticsSeed.SeedAsync(db, seed => seed.Employees.AddRange(
            secret,
            AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ana", new DateOnly(2020, 1, 1)),
            AnalyticsSeed.Employee(AnalyticsSeed.Ops, "Cy", new DateOnly(2020, 1, 1))));
        var range = AnalyticsSeed.Range(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), AnalyticsSeed.Sales);

        var cleared = await AnalyticsSeed.RunAsync(db, uow => new WorkforceAnalyticsService(uow).GetAsync(range, default));
        var uncleared = await AnalyticsSeed.RunAsync(db, uow => new WorkforceAnalyticsService(uow).GetAsync(range, default), new AnalyticsSeed.Uncleared(secret.Id));

        cleared.Kpi("headcount").Should().Be(2);
        uncleared.Kpi("headcount").Should().Be(1);
    }
}

public class AttendanceAnalyticsServiceTests
{
    private static DailyRecord Day(Employee e, DateOnly date, bool posted = true, int absent = 0, double late = 0, double overtime = 0) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = e.Id, FullName = e.FirstName, DepartmentId = e.DepartmentId, WorkDate = date,
        BatchCode = "B1", Posted = posted, AbsentCount = absent, RegularNetHours = absent > 0 ? 0 : 8,
        LateMinutes = late, RegularOTHours = overtime,
    };

    [Fact]
    public async Task OnlyPostedDaysCount_AndRatesAreFromScheduledDays()
    {
        using var db = new SqliteHrmsContext();
        var ana = AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ana", new DateOnly(2020, 1, 1));
        var cy = AnalyticsSeed.Employee(AnalyticsSeed.Ops, "Cy", new DateOnly(2020, 1, 1));
        await AnalyticsSeed.SeedAsync(db, seed =>
        {
            seed.Employees.AddRange(ana, cy);
            seed.DailyTimeRecords.AddRange(
                Day(ana, new DateOnly(2026, 3, 2), late: 30),
                Day(ana, new DateOnly(2026, 3, 3), late: 90, overtime: 2),
                Day(ana, new DateOnly(2026, 3, 4), absent: 1),
                Day(cy, new DateOnly(2026, 3, 2)),
                Day(cy, new DateOnly(2026, 3, 3), posted: false, absent: 1, late: 600),
                Day(ana, new DateOnly(2026, 2, 27), late: 60));
        });

        var result = await AnalyticsSeed.RunAsync(db, uow => new AttendanceAnalyticsService(uow).GetAsync(AnalyticsSeed.Range(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)), default));

        result.Kpi("attendanceRate").Should().Be(75m, "3 of 4 posted scheduled days worked");
        result.Kpi("lateHours").Should().Be(2m);
        result.Kpi("absences").Should().Be(1);
        result.Kpi("overtimeHours").Should().Be(2m);
        result.PreviousKpi("lateHours").Should().Be(1m, "the February day falls in the previous period");
        result.Breakdown("mostLate").Rows.Should().ContainSingle().Which.Should().Match<AnalyticsRow>(r => r.Label == "Ana" && r.Key == ana.Id.ToString());
        result.Breakdown("department").Rows.Select(r => (r.Label, r.Key)).Should().BeEquivalentTo(
            [("Sales", AnalyticsSeed.Sales.ToString()), ("Operations", AnalyticsSeed.Ops.ToString())]);
    }
}

public class PayrollAnalyticsServiceTests
{
    private static Payroll Pay(Employee e, DateOnly periodEnd, decimal gross, ApprovalStatus status = ApprovalStatus.Approved, PayrollType type = PayrollType.Regular) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = e.Id, PayPeriodEnd = periodEnd, PayrollDate = periodEnd, ApprovalStatus = status,
        PayrollType = type, GrossPay = gross, NetPay = gross * 0.8m, OvertimePay = gross * 0.1m,
        EmployerSSSContribution = 100m, EmployerPhilHealthContribution = 50m, EmployerPagIbigContribution = 20m, EmployerECContribution = 10m,
    };

    [Fact]
    public async Task OnlyApprovedRunsCount_AndLaborCostAddsEmployerShare()
    {
        using var db = new SqliteHrmsContext();
        var ana = AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ana", new DateOnly(2020, 1, 1));
        var secret = AnalyticsSeed.Employee(AnalyticsSeed.Ops, "Sam", new DateOnly(2020, 1, 1), confidential: true);
        await AnalyticsSeed.SeedAsync(db, seed =>
        {
            seed.Employees.AddRange(ana, secret);
            seed.Payrolls.AddRange(
                Pay(ana, new DateOnly(2026, 3, 15), 10_000m),
                Pay(ana, new DateOnly(2026, 3, 31), 10_000m),
                Pay(ana, new DateOnly(2026, 3, 31), 5_000m, ApprovalStatus.ForApproval),
                Pay(ana, new DateOnly(2026, 3, 31), 8_000m, type: PayrollType.ThirteenthMonth),
                Pay(secret, new DateOnly(2026, 3, 31), 50_000m));
        });
        var range = AnalyticsSeed.Range(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

        var uncleared = await AnalyticsSeed.RunAsync(db, uow => new PayrollAnalyticsService(uow).GetAsync(range, default), new AnalyticsSeed.Uncleared(secret.Id));

        uncleared.Kpi("gross").Should().Be(28_000m, "the draft run and the confidential employee are left out");
        uncleared.Kpi("employerContributions").Should().Be(540m);
        uncleared.Kpi("laborCost").Should().Be(28_540m);
        uncleared.Kpi("overtimeShare").Should().Be(10m);
        uncleared.Breakdown("runType").Rows.Select(r => r.Label).Should().Equal("Regular", "13th Month");
        uncleared.Breakdown("runType").Rows.Select(r => r.Key).Should().Equal("Regular", "ThirteenthMonth");
        uncleared.Breakdown("department").Rows.Should().ContainSingle().Which.Should().Match<AnalyticsRow>(r => r.Label == "Sales" && r.Key == AnalyticsSeed.Sales.ToString());

        var cleared = await AnalyticsSeed.RunAsync(db, uow => new PayrollAnalyticsService(uow).GetAsync(range, default));
        cleared.Kpi("gross").Should().Be(78_000m);
    }
}

public class RequestAnalyticsServiceTests
{
    [Fact]
    public void LeaveDays_CountCalendarDays_HalvedForAHalfDay()
    {
        RequestAnalyticsService.LeaveDays(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 4), DayFraction.FullDay).Should().Be(3);
        RequestAnalyticsService.LeaveDays(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 2), DayFraction.AM).Should().Be(0.5m);
    }

    [Fact]
    public void Median_OfOddAndEvenCounts()
    {
        RequestAnalyticsService.Median([5, 1, 3]).Should().Be(3);
        RequestAnalyticsService.Median([1, 2, 3, 4]).Should().Be(2.5m);
        RequestAnalyticsService.Median([]).Should().Be(0);
    }

    [Fact]
    public async Task CountsFiledApprovedAndWaiting_WithTimeToDecide_AndApprovedLeaveDays()
    {
        using var db = new SqliteHrmsContext();
        var ana = AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Ana", new DateOnly(2020, 1, 1));
        var boss = AnalyticsSeed.Employee(AnalyticsSeed.Sales, "Boss", new DateOnly(2020, 1, 1));
        var filed = new DateTime(2026, 3, 2, 8, 0, 0);
        var vacation = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };

        ApprovalInstance Request(ApprovalApplicationType type, ApprovalInstanceStatus status, double? decidedAfterHours)
        {
            var instance = new ApprovalInstance
            {
                Id = Guid.NewGuid(), ApplicationType = type, ApplicationId = Guid.NewGuid(), ApplicantEmployeeId = ana.Id,
                CurrentStepNumber = 1, Status = status, CreatedAt = filed,
            };
            if (decidedAfterHours is { } hours)
                instance.Actions.Add(new ApprovalAction
                {
                    Id = Guid.NewGuid(), StepNumber = 1, ActorEmployeeId = boss.Id, CreatedAt = filed.AddHours(hours),
                    Action = status == ApprovalInstanceStatus.Declined ? ApprovalActionType.Declined : ApprovalActionType.Approved,
                });
            return instance;
        }

        await AnalyticsSeed.SeedAsync(db, seed =>
        {
            seed.Employees.AddRange(ana, boss);
            seed.Leaves.Add(vacation);
            seed.ApprovalInstances.AddRange(
                Request(ApprovalApplicationType.Leave, ApprovalInstanceStatus.Approved, 2),
                Request(ApprovalApplicationType.Leave, ApprovalInstanceStatus.Approved, 6),
                Request(ApprovalApplicationType.Overtime, ApprovalInstanceStatus.Declined, 10),
                Request(ApprovalApplicationType.Overtime, ApprovalInstanceStatus.InProgress, null),
                // A payroll batch is not an employee request.
                Request(ApprovalApplicationType.PayrollPosting, ApprovalInstanceStatus.InProgress, null));
            seed.leaveApplications.AddRange(
                new LeaveApplication
                {
                    Id = Guid.NewGuid(), EmployeeId = ana.Id, LeaveId = vacation.Id, Leave = null!, DurationType = DurationType.MultiDay,
                    LeaveDateFrom = new DateOnly(2026, 3, 9), LeaveDateTo = new DateOnly(2026, 3, 11), ApprovalStatus = ApprovalStatus.Approved,
                },
                new LeaveApplication
                {
                    Id = Guid.NewGuid(), EmployeeId = ana.Id, LeaveId = vacation.Id, Leave = null!, DurationType = DurationType.MultiDay,
                    LeaveDateFrom = new DateOnly(2026, 3, 20), LeaveDateTo = new DateOnly(2026, 3, 20), ApprovalStatus = ApprovalStatus.ForApproval,
                });
        });

        var result = await AnalyticsSeed.RunAsync(db, uow => new RequestAnalyticsService(uow).GetAsync(AnalyticsSeed.Range(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31)), default));

        result.Kpi("filed").Should().Be(4);
        result.Kpi("approvalRate").Should().Be(66.7m, "2 approved of 3 decided");
        result.Kpi("medianHours").Should().Be(6m);
        result.Kpi("pending").Should().Be(1);
        result.Kpi("leaveDays").Should().Be(3m, "only the approved leave counts");
        result.Breakdown("approver").Rows.Should().ContainSingle().Which.Values["decisions"].Should().Be(3);
        result.Breakdown("leaveType").Rows.Should().ContainSingle().Which.Should().Match<AnalyticsRow>(r => r.Label == "Vacation Leave" && r.Key == vacation.Id.ToString());
        // Type rows share the series keys, so a row and a chart point drill the same way.
        result.Breakdown("type").Rows.Select(r => r.Key).Should().BeEquivalentTo(["leave", "overtime"]);
        // The API camelCases dictionary keys, so series keys must already be camelCase to match.
        result.SeriesDefinitions.Select(s => s.Key).Should().Contain("officialBusiness")
            .And.BeEquivalentTo(result.Series[0].Values.Keys);
        result.Series[0].Values["overtime"].Should().Be(2);
    }
}
