using Hrms.Core.Policies.OtherIncome.Eligibility;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>
/// Attendance-based Other Income: a per-day allowance is paid only for the days the DTR qualifies,
/// a weekly/monthly one only when its window's gate is met. A day that doesn't qualify is skipped --
/// stamped as consumed so it never carries into a later run -- and never reaches a payslip.
/// </summary>
public class OtherIncomeEligibilityTests
{
    // 2026-09-14 is a Monday.
    private static readonly DateOnly Monday = new(2026, 9, 14);
    private static readonly DateOnly Tuesday = Monday.AddDays(1);
    private static readonly DateOnly Wednesday = Monday.AddDays(2);
    private static readonly DateOnly Sunday = Monday.AddDays(6);

    private static OtherIncomeRule Rule(
        OtherIncomeAttendanceBasis basis = OtherIncomeAttendanceBasis.Worked,
        decimal? minHours = null, int? maxLate = null, int? maxUndertime = null,
        OtherIncomeWeekdays days = OtherIncomeWeekdays.All,
        OtherIncomeDayTypes dayTypes = OtherIncomeDayTypes.All,
        int? minQualifyingDays = null,
        OtherIncomePayMode mode = OtherIncomePayMode.PerQualifyingDay) =>
        new(mode, basis, minHours, maxLate, maxUndertime, (int)days, (int)dayTypes, minQualifyingDays);

    private static DailyRecord Worked(DateOnly date, double hours = 8, WorkType type = WorkType.RegularWorkDay) => new()
    {
        Id = Guid.NewGuid(), WorkDate = date, WorkTypeEnum = type, RegularNetHours = hours, Posted = true,
    };

    private static DailyRecord Absent(DateOnly date) => new()
    {
        Id = Guid.NewGuid(), WorkDate = date, WorkTypeEnum = WorkType.Absent, Posted = true,
    };

    private static DailyRecord PaidLeave(DateOnly date) => new()
    {
        Id = Guid.NewGuid(), WorkDate = date, WorkTypeEnum = WorkType.PaidLeave, PaidLeaveHours = 8, Posted = true,
    };

    private static string? Judge(OtherIncomeRule rule, DateOnly date, DailyRecord? record) =>
        DayEligibilityChain.Full.Evaluate(new DayContext(date, record, rule));

    // ---------------------------------------------------------------- the chain

    [Theory]
    [InlineData(DayOfWeek.Monday, 1)]
    [InlineData(DayOfWeek.Tuesday, 2)]
    [InlineData(DayOfWeek.Saturday, 32)]
    [InlineData(DayOfWeek.Sunday, 64)]
    public void WeekdayBit_MatchesTheOtherIncomeWeekdaysMask(DayOfWeek day, int bit) =>
        ApplicableWeekdayRule.WeekdayBit(day).Should().Be(bit);

    [Fact]
    public void AWorkedDay_Qualifies() =>
        Judge(Rule(), Monday, Worked(Monday)).Should().BeNull();

    [Fact]
    public void ADayWithNoPostedDtr_IsSkippedAsNoDtr() =>
        Judge(Rule(), Monday, null).Should().Be(OtherIncomeSkipReasons.NoDtr);

    [Fact]
    public void AnAbsentDay_IsSkippedAsNotWorked() =>
        Judge(Rule(), Monday, Absent(Monday)).Should().Be(OtherIncomeSkipReasons.NotWorked);

    [Fact]
    public void PaidLeave_QualifiesOnlyWhenTheBasisAcceptsIt()
    {
        Judge(Rule(OtherIncomeAttendanceBasis.WorkedOrPaidLeave), Monday, PaidLeave(Monday)).Should().BeNull();
        Judge(Rule(OtherIncomeAttendanceBasis.Worked), Monday, PaidLeave(Monday))
            .Should().Be(OtherIncomeSkipReasons.NotWorked);
    }

    [Fact]
    public void WithBasisNone_NoAttendanceIsRequired() =>
        Judge(Rule(OtherIncomeAttendanceBasis.None), Monday, Absent(Monday)).Should().BeNull();

    [Fact]
    public void MinHours_RequiresThatManyWorkedHours()
    {
        var rule = Rule(minHours: 8m);
        Judge(rule, Monday, Worked(Monday, 8)).Should().BeNull("exactly the minimum qualifies");
        Judge(rule, Monday, Worked(Monday, 7.5)).Should().Be(OtherIncomeSkipReasons.BelowMinHours);
    }

    [Fact]
    public void MinHours_DoesNotApplyToAPaidLeaveDayTheBasisAccepts() =>
        Judge(Rule(OtherIncomeAttendanceBasis.WorkedOrPaidLeave, minHours: 8m), Monday, PaidLeave(Monday))
            .Should().BeNull("leave is not worked hours");

    [Fact]
    public void LateAndUndertimeLimits_AreInclusiveAndZeroMeansNoTolerance()
    {
        var late = Worked(Monday);
        late.LateMinutes = 5;
        Judge(Rule(maxLate: 0), Monday, late).Should().Be(OtherIncomeSkipReasons.TooLate);
        Judge(Rule(maxLate: 5), Monday, late).Should().BeNull();
        Judge(Rule(), Monday, late).Should().BeNull("an unset limit isn't checked");

        var early = Worked(Monday);
        early.UTMinutes = 30;
        Judge(Rule(maxUndertime: 15), Monday, early).Should().Be(OtherIncomeSkipReasons.TooMuchUndertime);
    }

    [Fact]
    public void AWeekdayOutsideTheMask_IsNotApplicable() =>
        Judge(Rule(days: OtherIncomeWeekdays.All & ~OtherIncomeWeekdays.Sunday), Sunday, Worked(Sunday))
            .Should().Be(OtherIncomeSkipReasons.NotApplicableDay);

    [Theory]
    [InlineData(WorkType.RestDayDuty, OtherIncomeDayTypes.RestDay)]
    [InlineData(WorkType.LegalHolidayDuty, OtherIncomeDayTypes.LegalHoliday)]
    [InlineData(WorkType.RestDayLegalHolidayDuty, OtherIncomeDayTypes.LegalHoliday)]
    [InlineData(WorkType.SpecialHolidayDuty, OtherIncomeDayTypes.SpecialHoliday)]
    [InlineData(WorkType.RestDaySpecialHolidayDuty, OtherIncomeDayTypes.SpecialHoliday)]
    [InlineData(WorkType.RegularWorkDay, OtherIncomeDayTypes.Regular)]
    [InlineData(WorkType.SpecialWorkingHoliday, OtherIncomeDayTypes.Regular)]
    public void WorkType_IsClassifiedIntoADayType(WorkType workType, OtherIncomeDayTypes expected) =>
        DayTypeRule.Classify(workType).Should().Be(expected);

    [Fact]
    public void ADayTypeTheRuleExcludes_IsSkipped()
    {
        var regularOnly = Rule(dayTypes: OtherIncomeDayTypes.Regular);
        Judge(regularOnly, Monday, Worked(Monday, type: WorkType.RestDayDuty))
            .Should().Be(OtherIncomeSkipReasons.DayTypeExcluded);
        Judge(regularOnly, Monday, Worked(Monday)).Should().BeNull();
    }

    // ------------------------------------------------------------ the period gate

    private static Dictionary<DateOnly, DailyRecord> Week(params DailyRecord[] records) =>
        records.ToDictionary(x => x.WorkDate);

    private static readonly OtherIncomeRule MonToFri = Rule(
        days: OtherIncomeWeekdays.Monday | OtherIncomeWeekdays.Tuesday | OtherIncomeWeekdays.Wednesday
              | OtherIncomeWeekdays.Thursday | OtherIncomeWeekdays.Friday,
        mode: OtherIncomePayMode.PeriodicGated);

    [Fact]
    public void Gate_IsMetWhenEveryExpectedDayQualified()
    {
        var records = Week(Enumerable.Range(0, 5).Select(i => Worked(Monday.AddDays(i))).ToArray());
        PeriodGate.Evaluate(Monday, Sunday, MonToFri, records).Should().BeNull();
    }

    [Fact]
    public void Gate_IsNotMetWhenOneExpectedDayIsAbsent()
    {
        var records = Week(
            Worked(Monday), Worked(Tuesday), Absent(Wednesday),
            Worked(Monday.AddDays(3)), Worked(Monday.AddDays(4)));
        PeriodGate.Evaluate(Monday, Sunday, MonToFri, records).Should().Be(OtherIncomeSkipReasons.GateNotMet);
    }

    [Fact]
    public void Gate_CountsAMissingDtrAsNotQualified()
    {
        var records = Week(Worked(Monday), Worked(Tuesday));
        PeriodGate.Evaluate(Monday, Sunday, MonToFri, records).Should().Be(OtherIncomeSkipReasons.GateNotMet);
    }

    [Fact]
    public void Gate_MinQualifyingDays_AllowsSomeAbsences()
    {
        var records = Week(
            Worked(Monday), Worked(Tuesday), Absent(Wednesday),
            Worked(Monday.AddDays(3)), Worked(Monday.AddDays(4)));
        var fourOfFive = MonToFri with { MinQualifyingDays = 4 };
        var fiveOfFive = MonToFri with { MinQualifyingDays = 5 };

        PeriodGate.Evaluate(Monday, Sunday, fourOfFive, records).Should().BeNull();
        PeriodGate.Evaluate(Monday, Sunday, fiveOfFive, records).Should().Be(OtherIncomeSkipReasons.GateNotMet);
    }

    [Fact]
    public void Gate_OnlyExpectsDaysTheRuleApplies()
    {
        // Saturday and Sunday aren't applicable, so no records for them is fine.
        var records = Week(Enumerable.Range(0, 5).Select(i => Worked(Monday.AddDays(i))).ToArray());
        PeriodGate.Evaluate(Monday, Sunday, MonToFri, records).Should().BeNull();
    }

    [Fact]
    public void Gate_WithNoExpectedDaysInTheWindow_IsNotMet() =>
        PeriodGate.Evaluate(Sunday, Sunday, MonToFri, Week()).Should().Be(OtherIncomeSkipReasons.GateNotMet);

    // ------------------------------------------------------- against the database

    private sealed record Seeded(Guid EmployeeId, Guid IncomeId, Guid ApplicationId);

    private static async Task<Seeded> SeedAsync(
        SqliteHrmsContext db, OtherIncomePayMode mode, OtherIncomeAttendanceBasis basis,
        AllowanceFrequency frequency = AllowanceFrequency.Daily, int? minQualifyingDays = null)
    {
        await using var seed = db.NewContext();
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
        seed.Employees.Add(employee);
        var income = new OtherIncome { Id = Guid.NewGuid(), Code = "ATT", Name = "Attendance Allowance", IncomeClass = IncomeClassType.Regular };
        seed.Allowances.Add(income);
        var application = new OtherIncomeApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, IncomeId = income.Id,
            PayMode = mode, AttendanceBasis = basis, FrequencyOfPayment = frequency,
            MinQualifyingDays = minQualifyingDays,
            ApplicableDays = (int)(OtherIncomeWeekdays.Monday | OtherIncomeWeekdays.Tuesday
                | OtherIncomeWeekdays.Wednesday | OtherIncomeWeekdays.Thursday | OtherIncomeWeekdays.Friday),
        };
        seed.OtherIncomeApplications.Add(application);
        await seed.SaveChangesAsync();
        return new Seeded(employee.Id, income.Id, application.Id);
    }

    private static OtherIncomeSchedules Row(Seeded s, DateOnly date, decimal amount = 100m,
        DateOnly? periodStart = null, DateOnly? periodEnd = null) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, IncomeId = s.IncomeId,
        OtherIncomeApplicationId = s.ApplicationId, Date = date, Amount = amount, IsTaxable = true,
        PeriodStart = periodStart, PeriodEnd = periodEnd,
    };

    private static async Task AddAsync(SqliteHrmsContext db, Seeded s, IEnumerable<OtherIncomeSchedules> rows,
        params DailyRecord[] records)
    {
        await using var seed = db.NewContext();
        seed.OtherIncomeApplicationDetails.AddRange(rows);
        foreach (var record in records) record.EmployeeId = s.EmployeeId;
        seed.DailyTimeRecords.AddRange(records);
        await seed.SaveChangesAsync();
    }

    // What PayrollProcessorService.GenerateAsync does once a run is saved. The unit of work holds
    // its own transaction, so the stamp only lands once it's committed.
    private static async Task SaveRunAsync(SqliteHrmsContext db, Payroll payroll)
    {
        await using var context = db.NewContext();
        var service = new PayrollInputConsumptionService(new UnitOfWorkService(context));
        await service.MarkConsumedByDateRangeAsync([payroll], CancellationToken.None);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FindSkipped_PerDay_FlagsOnlyTheDaysThatDontQualify()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PerQualifyingDay, OtherIncomeAttendanceBasis.Worked);
        var rows = new[] { Row(s, Monday), Row(s, Tuesday), Row(s, Wednesday) };
        // Monday worked, Tuesday absent, Wednesday has no DTR at all.
        await AddAsync(db, s, rows, Worked(Monday), Absent(Tuesday));

        await using var context = db.NewContext();
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            context, [s.EmployeeId], Monday, Sunday, CancellationToken.None);

        skipped.Should().HaveCount(2);
        skipped[rows[1].Id].Should().Be(OtherIncomeSkipReasons.NotWorked);
        skipped[rows[2].Id].Should().Be(OtherIncomeSkipReasons.NoDtr);
    }

    [Fact]
    public async Task FindSkipped_NeverTouchesAFixedSchedule()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.FixedSchedule, OtherIncomeAttendanceBasis.None);
        await AddAsync(db, s, [Row(s, Monday), Row(s, Tuesday)]);

        await using var context = db.NewContext();
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            context, [s.EmployeeId], Monday, Sunday, CancellationToken.None);

        skipped.Should().BeEmpty("a fixed schedule is paid on its date, no DTR involved");
    }

    // An application created before the attendance-rule migration: the new string columns came in
    // as '' and the masks as 0. The converter reads '' as FixedSchedule, but SQL sees the raw ''.
    private static async Task<Seeded> SeedLegacyAsync(SqliteHrmsContext db)
    {
        var s = await SeedAsync(db, OtherIncomePayMode.FixedSchedule, OtherIncomeAttendanceBasis.None);
        await using var context = db.NewContext();
        await context.Database.ExecuteSqlRawAsync(
            "UPDATE OtherIncomeApplications SET PayMode = '', AttendanceBasis = '', ApplicableDays = 0, DayTypes = 0");
        return s;
    }

    [Fact]
    public async Task FindSkipped_LegacyBlankPayMode_IsTreatedAsFixed()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedLegacyAsync(db);
        await AddAsync(db, s, [Row(s, Monday), Row(s, Tuesday)]);

        await using var context = db.NewContext();
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            context, [s.EmployeeId], Monday, Sunday, CancellationToken.None);

        skipped.Should().BeEmpty("a blank pay mode is a fixed schedule");
    }

    [Fact]
    public async Task LoadAsync_PaysLegacyBlankPayModeRows()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedLegacyAsync(db);
        var rows = new[] { Row(s, Monday), Row(s, Tuesday) };
        await AddAsync(db, s, rows);

        await using var context = db.NewContext();
        var result = await new IncomeAplDtlService(new UnitOfWorkService(context))
            .LoadAsync([s.EmployeeId], Monday, Sunday, CancellationToken.None);

        result[new EmployeeKey(s.EmployeeId)].Select(x => x.Id).Should().BeEquivalentTo(rows.Select(x => x.Id));
    }

    [Fact]
    public async Task MarkConsumed_DoesNotFlagLegacyBlankPayModeRows()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedLegacyAsync(db);
        await AddAsync(db, s, [Row(s, Monday), Row(s, Tuesday)]);
        var payroll = new Payroll { Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, PayPeriodStart = Monday, PayPeriodEnd = Sunday };

        await SaveRunAsync(db, payroll);

        await using var check = db.NewContext();
        var stored = await check.OtherIncomeApplicationDetails.AsNoTracking().ToListAsync();
        stored.Should().OnlyContain(x => x.ConsumedByPayrollId == payroll.Id && !x.IsSkipped);
    }

    [Fact]
    public async Task FindSkipped_IgnoresUnpostedDtr()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PerQualifyingDay, OtherIncomeAttendanceBasis.Worked);
        var row = Row(s, Monday);
        var draft = Worked(Monday);
        draft.Posted = false;
        await AddAsync(db, s, [row], draft);

        await using var context = db.NewContext();
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            context, [s.EmployeeId], Monday, Sunday, CancellationToken.None);

        skipped[row.Id].Should().Be(OtherIncomeSkipReasons.NoDtr, "only posted DTR counts as attendance");
    }

    [Fact]
    public async Task FindSkipped_APeriodicWindow_ReadsDaysAnEarlierRunAlreadyPaid()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PeriodicGated, OtherIncomeAttendanceBasis.Worked,
            AllowanceFrequency.Weekly);
        var row = Row(s, Sunday, 500m, Monday, Sunday);
        var records = Enumerable.Range(0, 5).Select(i => Worked(Monday.AddDays(i))).ToArray();
        // The first three days were paid by a previous payroll run; the gate must still see them.
        foreach (var paid in records.Take(3)) paid.PayrollBatchId = Guid.NewGuid();
        await AddAsync(db, s, [row], records);

        await using var context = db.NewContext();
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            context, [s.EmployeeId], Sunday, Sunday, CancellationToken.None);

        skipped.Should().BeEmpty("all five expected days were worked, paid-by-an-earlier-run or not");
    }

    [Fact]
    public async Task FindSkipped_APeriodicWindowWithAnAbsence_IsSkippedAsGateNotMet()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PeriodicGated, OtherIncomeAttendanceBasis.Worked,
            AllowanceFrequency.Weekly);
        var row = Row(s, Sunday, 500m, Monday, Sunday);
        await AddAsync(db, s, [row],
            Worked(Monday), Worked(Tuesday), Absent(Wednesday), Worked(Monday.AddDays(3)), Worked(Monday.AddDays(4)));

        await using var context = db.NewContext();
        var skipped = await OtherIncomeEligibilityEvaluator.FindSkippedAsync(
            context, [s.EmployeeId], Sunday, Sunday, CancellationToken.None);

        skipped[row.Id].Should().Be(OtherIncomeSkipReasons.GateNotMet);
    }

    [Fact]
    public async Task LoadAsync_PaysOnlyTheQualifyingDays()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PerQualifyingDay, OtherIncomeAttendanceBasis.Worked);
        var paid = Row(s, Monday);
        await AddAsync(db, s, [paid, Row(s, Tuesday)], Worked(Monday), Absent(Tuesday));

        await using var context = db.NewContext();
        var result = await new IncomeAplDtlService(new UnitOfWorkService(context))
            .LoadAsync([s.EmployeeId], Monday, Sunday, CancellationToken.None);

        result[new EmployeeKey(s.EmployeeId)].Select(x => x.Id).Should().Equal(paid.Id);
    }

    [Fact]
    public async Task MarkConsumed_StampsSkippedRowsToo_SoTheyNeverCarryOver()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PerQualifyingDay, OtherIncomeAttendanceBasis.Worked);
        var paid = Row(s, Monday);
        var skippedRow = Row(s, Tuesday);
        await AddAsync(db, s, [paid, skippedRow], Worked(Monday), Absent(Tuesday));
        var payroll = new Payroll { Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, PayPeriodStart = Monday, PayPeriodEnd = Sunday };

        await SaveRunAsync(db, payroll);

        await using var check = db.NewContext();
        var rows = await check.OtherIncomeApplicationDetails.AsNoTracking().ToListAsync();
        rows.Should().OnlyContain(x => x.ConsumedByPayrollId == payroll.Id);
        rows.Single(x => x.Id == paid.Id).IsSkipped.Should().BeFalse();
        var skippedStored = rows.Single(x => x.Id == skippedRow.Id);
        skippedStored.IsSkipped.Should().BeTrue();
        skippedStored.SkipReason.Should().Be(OtherIncomeSkipReasons.NotWorked);
    }

    [Fact]
    public async Task Release_ClearsTheSkipVerdictSoTheNextRunJudgesItAgain()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PerQualifyingDay, OtherIncomeAttendanceBasis.Worked);
        var row = Row(s, Tuesday);
        await AddAsync(db, s, [row], Absent(Tuesday));
        var payroll = new Payroll { Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, PayPeriodStart = Monday, PayPeriodEnd = Sunday };

        await SaveRunAsync(db, payroll);
        await using (var context = db.NewContext())
        {
            var service = new PayrollInputConsumptionService(new UnitOfWorkService(context));
            await service.ReleaseAsync([payroll.Id], CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        await using var check = db.NewContext();
        var stored = await check.OtherIncomeApplicationDetails.AsNoTracking().SingleAsync();
        stored.ConsumedByPayrollId.Should().BeNull();
        stored.IsSkipped.Should().BeFalse();
        stored.SkipReason.Should().BeNull();
    }

    [Fact]
    public async Task Payslip_LeavesOutSkippedRows()
    {
        using var db = new SqliteHrmsContext();
        var s = await SeedAsync(db, OtherIncomePayMode.PerQualifyingDay, OtherIncomeAttendanceBasis.Worked);
        await AddAsync(db, s, [Row(s, Monday, 100m), Row(s, Tuesday, 100m)], Worked(Monday), Absent(Tuesday));
        var payroll = new Payroll { Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, PayPeriodStart = Monday, PayPeriodEnd = Sunday };

        await SaveRunAsync(db, payroll);

        await using var read = db.NewContext();
        var lines = await new IncomeAplDtlService(new UnitOfWorkService(read))
            .GetPayslipLinesAsync(payroll.Id, CancellationToken.None);

        lines.Should().ContainSingle().Which.Amount.Should().Be(100m, "the skipped Tuesday isn't paid");
    }
}
