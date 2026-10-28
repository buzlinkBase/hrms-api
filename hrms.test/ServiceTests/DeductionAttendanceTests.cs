using Hrms.Core.Policies.OtherIncome.Eligibility;
using Hrms.Core.Services.Approvals;
using Hrms.Core.Services.Schedules;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Attendance-based Loans &amp; Deductions: a per-day charge (meals, transport, ...) is taken only
/// for the days the DTR qualifies. A day that doesn't is either skipped for good or deferred to
/// the next run, as the application says. The shared rule chain is covered by
/// OtherIncomeEligibilityTests; these cover what is specific to deductions -- installments carry
/// over by design, are paid down at Post, and the setup must allow the rules.
/// </summary>
public class DeductionAttendanceTests : IDisposable
{
    // 2026-09-14 is a Monday.
    private static readonly DateOnly Monday = new(2026, 9, 14);
    private static readonly DateOnly Tuesday = Monday.AddDays(1);
    private static readonly DateOnly Wednesday = Monday.AddDays(2);
    private static readonly DateOnly Sunday = Monday.AddDays(6);

    private readonly SqliteHrmsContext _db = new();

    public void Dispose() => _db.Dispose();

    private sealed record Seeded(Guid EmployeeId, Guid DeductionId, Guid ApplicationId);

    private async Task<Seeded> SeedAsync(
        OtherIncomePayMode mode = OtherIncomePayMode.PerQualifyingDay,
        AttendanceMissAction onMiss = AttendanceMissAction.Skip,
        bool allowRules = true,
        ApprovalStatus approval = ApprovalStatus.Approved)
    {
        await using var seed = _db.NewContext();
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
        seed.Employees.Add(employee);
        var deduction = new Deduction { Id = Guid.NewGuid(), Code = "MEAL", Name = "Meal", AllowAttendanceRules = allowRules };
        seed.Deductions.Add(deduction);
        var application = new DeductionApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, DeductionId = deduction.Id,
            ApprovalStatus = approval, PayMode = mode, AttendanceBasis = OtherIncomeAttendanceBasis.Worked,
            OnMiss = onMiss, Note = "", Remarks = "",
            ApplicableDays = (int)(OtherIncomeWeekdays.Monday | OtherIncomeWeekdays.Tuesday
                | OtherIncomeWeekdays.Wednesday | OtherIncomeWeekdays.Thursday | OtherIncomeWeekdays.Friday),
        };
        seed.DeductionApplications.Add(application);
        await seed.SaveChangesAsync();
        return new Seeded(employee.Id, deduction.Id, application.Id);
    }

    private static DeductionApplicationDetail Row(Seeded s, DateOnly date, int order, decimal amount = 50m) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, DeductionId = s.DeductionId,
        DeductionApplicationId = s.ApplicationId, Date = date, Principal = amount, Amount = amount,
        Balance = amount, RecordOrder = order,
    };

    private static DailyRecord Worked(DateOnly date) => new()
    {
        Id = Guid.NewGuid(), WorkDate = date, WorkTypeEnum = WorkType.RegularWorkDay, RegularNetHours = 8, Posted = true,
    };

    private static DailyRecord Absent(DateOnly date) => new()
    {
        Id = Guid.NewGuid(), WorkDate = date, WorkTypeEnum = WorkType.Absent, Posted = true,
    };

    private async Task AddAsync(Seeded s, IEnumerable<DeductionApplicationDetail> rows, params DailyRecord[] records)
    {
        await using var seed = _db.NewContext();
        seed.DeductionApplicationDetails.AddRange(rows);
        foreach (var record in records) record.EmployeeId = s.EmployeeId;
        seed.DailyTimeRecords.AddRange(records);
        await seed.SaveChangesAsync();
    }

    private static Payroll PayrollFor(Seeded s) => new()
    {
        Id = Guid.NewGuid(), EmployeeId = s.EmployeeId, PayPeriodStart = Monday, PayPeriodEnd = Sunday,
    };

    // What PayrollProcessorService.GenerateAsync does once a run is saved.
    private async Task SaveRunAsync(Payroll payroll)
    {
        await using var context = _db.NewContext();
        var service = new PayrollInputConsumptionService(new UnitOfWorkService(context));
        await service.MarkConsumedByDateRangeAsync([payroll], CancellationToken.None);
        await service.CommitChangesAsync(CancellationToken.None);
    }

    private async Task<List<Guid>> LoadedIdsAsync(Seeded s, DateOnly toDate)
    {
        await using var context = _db.NewContext();
        var result = await new DeductionAplDtlService(new UnitOfWorkService(context))
            .LoadAsync([s.EmployeeId], Monday, toDate, CancellationToken.None);
        return result.TryGetValue(new EmployeeKey(s.EmployeeId), out var list)
            ? list.Select(x => x.Id).ToList()
            : [];
    }

    private async Task<Dictionary<Guid, DeductionMiss>> MissesAsync(Seeded s, DateOnly toDate)
    {
        await using var context = _db.NewContext();
        return await DeductionEligibilityEvaluator.FindMissesAsync(context, [s.EmployeeId], toDate, CancellationToken.None);
    }

    // ------------------------------------------------------------------ evaluator

    [Fact]
    public async Task FindMisses_FlagsOnlyTheDaysThatDontQualify()
    {
        var s = await SeedAsync();
        var rows = new[] { Row(s, Monday, 1), Row(s, Tuesday, 2), Row(s, Wednesday, 3) };
        // Monday worked, Tuesday absent, Wednesday has no DTR.
        await AddAsync(s, rows, Worked(Monday), Absent(Tuesday));

        var misses = await MissesAsync(s, Sunday);

        misses.Should().HaveCount(2);
        misses[rows[1].Id].Reason.Should().Be(OtherIncomeSkipReasons.NotWorked);
        misses[rows[2].Id].Reason.Should().Be(OtherIncomeSkipReasons.NoDtr);
        misses.Values.Should().OnlyContain(x => x.Action == AttendanceMissAction.Skip);
    }

    [Fact]
    public async Task FindMisses_CarriesTheApplicationsChoiceBetweenSkipAndDefer()
    {
        var s = await SeedAsync(onMiss: AttendanceMissAction.Defer);
        var row = Row(s, Tuesday, 1);
        await AddAsync(s, [row], Absent(Tuesday));

        (await MissesAsync(s, Sunday))[row.Id].Action.Should().Be(AttendanceMissAction.Defer);
    }

    [Fact]
    public async Task FindMisses_LeavesFixedSchedulesUnapprovedLoansAndJudgedRowsAlone()
    {
        var fixedSchedule = await SeedAsync(OtherIncomePayMode.FixedSchedule);
        await AddAsync(fixedSchedule, [Row(fixedSchedule, Monday, 1)]);
        var pending = await SeedAsync(approval: ApprovalStatus.ForApproval);
        await AddAsync(pending, [Row(pending, Monday, 1)]);
        var judged = await SeedAsync();
        var skippedRow = Row(judged, Monday, 1);
        skippedRow.IsSkipped = true;
        var deferredRow = Row(judged, Tuesday, 2);
        deferredRow.IsDeferred = true;
        await AddAsync(judged, [skippedRow, deferredRow]);

        (await MissesAsync(fixedSchedule, Sunday)).Should().BeEmpty("a fixed schedule is deducted on its date");
        (await MissesAsync(pending, Sunday)).Should().BeEmpty("payroll never sees an unapproved application");
        (await MissesAsync(judged, Sunday)).Should().BeEmpty("a row a saved run already judged isn't judged again");
    }

    [Fact]
    public async Task FindMisses_APeriodicWindow_ReadsDaysAnEarlierRunAlreadyPaid()
    {
        var s = await SeedAsync(OtherIncomePayMode.PeriodicGated);
        var row = Row(s, Sunday, 1, 300m);
        row.PeriodStart = Monday;
        row.PeriodEnd = Sunday;
        var records = Enumerable.Range(0, 5).Select(i => Worked(Monday.AddDays(i))).ToArray();
        foreach (var paid in records.Take(3)) paid.PayrollBatchId = Guid.NewGuid();
        await AddAsync(s, [row], records);

        (await MissesAsync(s, Sunday)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ payroll read side

    [Fact]
    public async Task LoadAsync_ChargesOnlyTheQualifyingDays()
    {
        var s = await SeedAsync();
        var charged = Row(s, Monday, 1);
        await AddAsync(s, [charged, Row(s, Tuesday, 2)], Worked(Monday), Absent(Tuesday));

        (await LoadedIdsAsync(s, Sunday)).Should().Equal(charged.Id);
    }

    [Fact]
    public async Task LoadAsync_NeverLoadsASkippedRow_ButChargesADeferredOneWithoutJudgingItAgain()
    {
        var s = await SeedAsync();
        var skipped = Row(s, Monday, 1);
        skipped.IsSkipped = true;
        var deferred = Row(s, Tuesday, 2);
        deferred.IsDeferred = true;
        // The employee was absent on both days -- a deferred row is charged anyway.
        await AddAsync(s, [skipped, deferred], Absent(Monday), Absent(Tuesday));

        (await LoadedIdsAsync(s, Sunday)).Should().Equal(deferred.Id);
    }

    [Fact]
    public async Task LoadAsync_AFixedScheduleIsUnchanged()
    {
        var s = await SeedAsync(OtherIncomePayMode.FixedSchedule);
        var row = Row(s, Monday, 1);
        await AddAsync(s, [row]);

        (await LoadedIdsAsync(s, Sunday)).Should().Equal(row.Id);
    }

    // ------------------------------------------------------------------ payroll save side

    [Fact]
    public async Task SavingARun_SkipsAMissedDayForGood()
    {
        var s = await SeedAsync();
        var kept = Row(s, Monday, 1);
        var missed = Row(s, Tuesday, 2);
        await AddAsync(s, [kept, missed], Worked(Monday), Absent(Tuesday));
        var payroll = PayrollFor(s);

        await SaveRunAsync(payroll);

        await using var check = _db.NewContext();
        var rows = await check.DeductionApplicationDetails.AsNoTracking().ToListAsync();
        rows.Single(x => x.Id == kept.Id).IsSkipped.Should().BeFalse();
        var stored = rows.Single(x => x.Id == missed.Id);
        stored.IsSkipped.Should().BeTrue();
        stored.IsDeferred.Should().BeFalse();
        stored.SkipReason.Should().Be(OtherIncomeSkipReasons.NotWorked);
        stored.SkippedByPayrollId.Should().Be(payroll.Id);
        // A skipped day never carries into a later run.
        (await LoadedIdsAsync(s, Sunday.AddDays(14))).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task SavingARun_DefersAMissedDay_SoTheNextRunChargesIt()
    {
        var s = await SeedAsync(onMiss: AttendanceMissAction.Defer);
        var missed = Row(s, Tuesday, 1);
        await AddAsync(s, [missed], Absent(Tuesday));

        await SaveRunAsync(PayrollFor(s));

        await using var check = _db.NewContext();
        var stored = await check.DeductionApplicationDetails.AsNoTracking().SingleAsync();
        stored.IsDeferred.Should().BeTrue();
        stored.IsSkipped.Should().BeFalse();
        (await LoadedIdsAsync(s, Sunday.AddDays(14))).Should().Equal(missed.Id);
    }

    [Fact]
    public async Task DeletingTheRunsBatch_ReleasesWhatItSkippedOrDeferred()
    {
        var s = await SeedAsync();
        var row = Row(s, Tuesday, 1);
        await AddAsync(s, [row], Absent(Tuesday));
        var payroll = PayrollFor(s);
        await SaveRunAsync(payroll);

        await using (var context = _db.NewContext())
        {
            var service = new PayrollInputConsumptionService(new UnitOfWorkService(context));
            await service.ReleaseAsync([payroll.Id], CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        await using var check = _db.NewContext();
        var stored = await check.DeductionApplicationDetails.AsNoTracking().SingleAsync();
        stored.IsSkipped.Should().BeFalse();
        stored.IsDeferred.Should().BeFalse();
        stored.SkipReason.Should().BeNull();
        stored.SkippedByPayrollId.Should().BeNull();
    }

    // ------------------------------------------------------------------ editing

    [Fact]
    public void ASkippedOrDeferredInstallment_IsLockedLikeAPaidOne()
    {
        var s = new Seeded(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var reconciler = new LoanScheduleReconciler(new HashSet<Guid>());

        reconciler.AnyLocked([Row(s, Monday, 1)]).Should().BeFalse();
        var skipped = Row(s, Monday, 1);
        skipped.IsSkipped = true;
        var deferred = Row(s, Monday, 1);
        deferred.IsDeferred = true;
        reconciler.AnyLocked([skipped]).Should().BeTrue();
        reconciler.AnyLocked([deferred]).Should().BeTrue();
    }

    // ------------------------------------------------------------------ saving an application

    private static DeductionApplicationService BuildService(IUnitOfWorkService uow)
    {
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)
        var employeeService = new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
        return new DeductionApplicationService(uow, new DeductionService(uow), employeeService,
            new Mapper(TypeAdapterConfig.GlobalSettings), new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()));
    }

    private static CreateDeductionApplication Form(Seeded s) => new()
    {
        EmployeeId = s.EmployeeId, DeductionId = s.DeductionId,
        EncodeDate = Monday, StartDate = Monday, EndDate = Sunday,
        FrequencyOfPayment = DeductionFrequency.Daily, Terms = 2, TotalPrincipal = 100m, TotalAmount = 100m,
        PayMode = OtherIncomePayMode.PerQualifyingDay,
        AttendanceBasis = OtherIncomeAttendanceBasis.Worked,
        MinHours = 8m,
        OnMiss = AttendanceMissAction.Defer,
        ApplicableDays = (int)(OtherIncomeWeekdays.Monday | OtherIncomeWeekdays.Tuesday),
        Breakdown =
        [
            new() { Date = Monday, Principal = 50m, Amount = 50m },
            new() { Date = Tuesday, Principal = 50m, Amount = 50m },
        ],
    };

    private async Task<DeductionApplication> AddAsync(CreateDeductionApplication form)
    {
        await using var context = _db.NewProductionLikeContext();
        return await BuildService(new UnitOfWorkService(context))
            .AddAsync(form, ApprovalStatus.Approved, CancellationToken.None);
    }

    [Fact]
    public async Task AddAsync_PersistsTheRuleAndEachRowsPeriod()
    {
        var s = await SeedAsync();
        var form = Form(s);
        form.PayMode = OtherIncomePayMode.PeriodicGated;
        form.FrequencyOfPayment = DeductionFrequency.Weekly;
        form.Breakdown = [new() { Date = Sunday, Principal = 100m, Amount = 100m, PeriodStart = Monday, PeriodEnd = Sunday }];

        var app = await AddAsync(form);

        app.PayMode.Should().Be(OtherIncomePayMode.PeriodicGated);
        app.OnMiss.Should().Be(AttendanceMissAction.Defer);
        app.MinHours.Should().Be(8m);
        app.ApplicableDays.Should().Be(3);
        app.Breakdown.Should().ContainSingle().Which.Should().Match<DeductionApplicationDetail>(
            x => x.PeriodStart == Monday && x.PeriodEnd == Sunday);
    }

    [Fact]
    public async Task AddAsync_RefusesAnAttendanceRuleOnADeductionNotSetUpForIt()
    {
        var s = await SeedAsync(allowRules: false);

        var act = () => AddAsync(Form(s));

        await act.Should().ThrowAsync<Exception>().WithMessage("*isn't set up for attendance-based rules*");
    }

    [Fact]
    public async Task AddAsync_RefusesInterestOnAnAttendanceDeduction()
    {
        var s = await SeedAsync();
        var form = Form(s);
        form.InterestRate = 5m;

        var act = () => AddAsync(form);

        await act.Should().ThrowAsync<Exception>().WithMessage("*can't carry interest*");
    }

    [Fact]
    public async Task AddAsync_RefusesAnAttendanceModeWithoutABasis()
    {
        var s = await SeedAsync();
        var form = Form(s);
        form.AttendanceBasis = OtherIncomeAttendanceBasis.None;

        var act = () => AddAsync(form);

        await act.Should().ThrowAsync<Exception>().WithMessage("*what counts as present*");
    }

    [Fact]
    public async Task AddAsync_AcceptsASemiMonthlyPeriodicGate()
    {
        var s = await SeedAsync();
        var form = Form(s);
        form.PayMode = OtherIncomePayMode.PeriodicGated;
        form.FrequencyOfPayment = DeductionFrequency.SemiMonthly;
        form.Breakdown = [new() { Date = Sunday, Principal = 100m, Amount = 100m, PeriodStart = Monday, PeriodEnd = Sunday }];

        var app = await AddAsync(form);

        app.FrequencyOfPayment.Should().Be(DeductionFrequency.SemiMonthly);
    }

    [Fact]
    public async Task AddAsync_RefusesAPeriodicGateThatIsNotWeeklyOrMonthly()
    {
        var s = await SeedAsync();
        var form = Form(s);
        form.PayMode = OtherIncomePayMode.PeriodicGated;
        form.FrequencyOfPayment = DeductionFrequency.Daily;

        var act = () => AddAsync(form);

        await act.Should().ThrowAsync<Exception>().WithMessage("*semi-monthly*");
    }

    [Fact]
    public async Task AddAsync_AFixedScheduleNeedsNoSetupFlag()
    {
        var s = await SeedAsync(allowRules: false);
        var form = Form(s);
        form.PayMode = OtherIncomePayMode.FixedSchedule;
        form.AttendanceBasis = OtherIncomeAttendanceBasis.None;

        var app = await AddAsync(form);

        app.PayMode.Should().Be(OtherIncomePayMode.FixedSchedule);
    }

    // ------------------------------------------------------------------ pre-migration applications

    // An application created before the attendance-rule migration: the new string columns came in
    // as '' and the masks as 0. The converter reads '' as the default, but SQL sees the raw ''.
    private async Task BlankRuleColumnsAsync(Guid applicationId)
    {
        await using var context = _db.NewContext();
        await context.Database.ExecuteSqlAsync(
            $"UPDATE DeductionApplications SET PayMode = '', AttendanceBasis = '', OnMiss = '', ApplicableDays = 0, DayTypes = 0 WHERE Id = {applicationId}");
    }

    [Fact]
    public async Task FindMisses_LegacyBlankPayMode_IsTreatedAsFixed()
    {
        var s = await SeedAsync(OtherIncomePayMode.FixedSchedule);
        await BlankRuleColumnsAsync(s.ApplicationId);
        await AddAsync(s, [Row(s, Monday, 1), Row(s, Tuesday, 2)], Absent(Monday), Absent(Tuesday));

        (await MissesAsync(s, Sunday)).Should().BeEmpty("a blank pay mode is a fixed schedule");
        (await LoadedIdsAsync(s, Sunday)).Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_RepairsALegacyBlankPayMode()
    {
        var s = await SeedAsync();
        var form = Form(s);
        form.PayMode = OtherIncomePayMode.FixedSchedule;
        form.AttendanceBasis = OtherIncomeAttendanceBasis.None;
        form.OnMiss = AttendanceMissAction.Skip;
        var app = await AddAsync(form);
        await BlankRuleColumnsAsync(app.Id);

        await using (var context = _db.NewProductionLikeContext())
        {
            await BuildService(new UnitOfWorkService(context)).UpdateAsync(new UpdateDeductionApplication
            {
                Id = app.Id, EmployeeId = s.EmployeeId, DeductionId = s.DeductionId,
                EncodeDate = Monday, StartDate = Monday, EndDate = Sunday,
                FrequencyOfPayment = DeductionFrequency.Daily, Terms = 2, TotalPrincipal = 100m, TotalAmount = 100m,
                PayMode = OtherIncomePayMode.FixedSchedule,
                AttendanceBasis = OtherIncomeAttendanceBasis.None,
                OnMiss = AttendanceMissAction.Skip,
                Details = app.Breakdown
                    .Select(x => new UpdateDeductionApplicationDetail
                    {
                        Id = x.Id, Date = x.Date, Principal = x.Principal, Amount = x.Amount,
                    })
                    .ToList(),
            }, CancellationToken.None);
        }

        await using var check = _db.NewContext();
        var stored = await check.Database
            .SqlQuery<string>(
                $"SELECT PayMode || '|' || AttendanceBasis || '|' || OnMiss AS Value FROM DeductionApplications WHERE Id = {app.Id}")
            .ToListAsync();
        stored.Should().Equal("FixedSchedule|None|Skip");
    }
}
