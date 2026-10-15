using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Other Income application save/update/delete, against a production-like context (soft-delete
/// interceptor + lazy proxies). Creating an application used to look up "existing" schedule rows
/// by the new application's still-empty Id, which matched every other application's rows and
/// soft-deleted them all -- payroll then found no Other Income. Editing then failed with a
/// duplicate primary key, because the rows were looked up by a column that held Guid.Empty.
/// </summary>
public class IncomeAplMainServiceTests : IDisposable
{
    private static readonly DateOnly Start = new(2026, 9, 30);

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _employeeId;
    private readonly Guid _incomeId;

    public IncomeAplMainServiceTests()
    {
        using var seed = _db.NewContext();
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        _employeeId = Guid.NewGuid();
        seed.Employees.Add(new Employee { Id = _employeeId, PayrollGroupId = groupId });
        _incomeId = Guid.NewGuid();
        seed.Allowances.Add(new OtherIncome
        {
            Id = _incomeId, Code = "HG", Name = "HG Allowance", IncomeClass = IncomeClassType.Regular, IsTaxable = true,
        });
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static IncomeAplMainService BuildService(IUnitOfWorkService uow)
    {
        _ = TestMapping.GlobalSettings; // registers MappingProfile once per run (see TestMapping)
        var employeeService = new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
        return new IncomeAplMainService(uow, new OtherIncomeService(uow), employeeService,
            new Mapper(TypeAdapterConfig.GlobalSettings));
    }

    private async Task<T> RunAsync<T>(Func<IncomeAplMainService, Task<T>> action)
    {
        await using var context = _db.NewProductionLikeContext();
        return await action(BuildService(new UnitOfWorkService(context)));
    }

    private async Task RunAsync(Func<IncomeAplMainService, Task> action)
    {
        await using var context = _db.NewProductionLikeContext();
        await action(BuildService(new UnitOfWorkService(context)));
    }

    private CreateOtherIncomeApplication CreateForm(decimal amount = 500m, int payments = 3) => new()
    {
        EmployeeId = _employeeId,
        IncomeId = _incomeId,
        EncodeDate = Start,
        StartDate = Start,
        EndDate = Start.AddMonths(payments),
        Amount = amount,
        Schedule = Enumerable.Range(0, payments)
            .Select(i => new CreateOtherIncomeSchedule { Date = Start.AddMonths(i), Amount = amount })
            .ToList(),
    };

    private static UpdateOtherIncomeApplication UpdateForm(OtherIncomeApplication stored,
        IEnumerable<UpdateOtherIncomeSchedule> schedule) => new()
    {
        Id = stored.Id,
        EmployeeId = stored.EmployeeId,
        IncomeId = stored.IncomeId,
        EncodeDate = stored.EncodeDate,
        StartDate = stored.StartDate,
        EndDate = stored.EndDate,
        Amount = stored.Amount,
        Schedule = schedule.ToList(),
    };

    private async Task<List<OtherIncomeSchedules>> AllRowsAsync()
    {
        await using var check = _db.NewContext();
        return await check.OtherIncomeApplicationDetails.IgnoreQueryFilters().AsNoTracking().ToListAsync();
    }

    private async Task MarkConsumedAsync(Guid rowId)
    {
        await using var context = _db.NewContext();
        var row = await context.OtherIncomeApplicationDetails.SingleAsync(x => x.Id == rowId);
        row.ConsumedByPayrollId = Guid.NewGuid();
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_LeavesEveryOtherApplicationsScheduleAlone()
    {
        var first = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));
        var second = await RunAsync(s => s.AddAsync(CreateForm(800m, 2), CancellationToken.None));

        var rows = await AllRowsAsync();
        rows.Should().HaveCount(5).And.OnlyContain(x => x.DeletedAt == null, "creating one application must not delete another's rows");
        rows.Count(x => x.OtherIncomeApplicationId == first.Id).Should().Be(3);
        rows.Count(x => x.OtherIncomeApplicationId == second.Id).Should().Be(2);
    }

    [Fact]
    public async Task AddAsync_CopiesTheApplicationsPayrollValuesOntoEachRow()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));

        var rows = await AllRowsAsync();
        rows.Should().OnlyContain(x =>
            x.OtherIncomeApplicationId == app.Id && x.EmployeeId == _employeeId && x.IncomeId == _incomeId &&
            x.IsTaxable);
    }

    [Fact]
    public async Task UpdateAsync_WithTheStoredRowIds_EditsThemInPlace()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));
        var stored = (await RunAsync(s => s.FineOneAsync(app.Id, CancellationToken.None)))!;
        var ids = stored.Schedule.Select(x => x.Id).ToList();

        var form = UpdateForm(stored, stored.Schedule.Select(x => new UpdateOtherIncomeSchedule { Id = x.Id, Date = x.Date, Amount = 650m }));
        var saved = await RunAsync(s => s.UpdateAsync(form, CancellationToken.None));

        saved.Schedule.Select(x => x.Id).Should().BeEquivalentTo(ids);
        var rows = await AllRowsAsync();
        rows.Should().HaveCount(3).And.OnlyContain(x => x.DeletedAt == null && x.Amount == 650m);
    }

    [Fact]
    public async Task UpdateAsync_WithARegeneratedSchedule_ReplacesTheOldRows()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));
        var stored = (await RunAsync(s => s.FineOneAsync(app.Id, CancellationToken.None)))!;

        var regenerated = Enumerable.Range(0, 2)
            .Select(i => new UpdateOtherIncomeSchedule { Date = Start.AddDays(15 * i), Amount = 250m });
        await RunAsync(s => s.UpdateAsync(UpdateForm(stored, regenerated), CancellationToken.None));

        var rows = await AllRowsAsync();
        rows.Select(x => x.Id).Should().NotIntersectWith(stored.Schedule.Select(x => x.Id), "replaced rows are deleted outright");
        rows.Should().HaveCount(2)
            .And.OnlyContain(x => x.DeletedAt == null && x.OtherIncomeApplicationId == app.Id && x.Amount == 250m);
    }

    [Fact]
    public async Task UpdateAsync_RefusesToChangeOrDropARowAlreadyPaidByPayroll()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));
        var stored = (await RunAsync(s => s.FineOneAsync(app.Id, CancellationToken.None)))!;
        var paid = stored.Schedule.First();
        await MarkConsumedAsync(paid.Id);

        var changed = UpdateForm(stored, stored.Schedule.Select(x => new UpdateOtherIncomeSchedule { Id = x.Id, Date = x.Date, Amount = 999m }));
        var dropped = UpdateForm(stored, stored.Schedule.Skip(1).Select(x => new UpdateOtherIncomeSchedule { Id = x.Id, Date = x.Date, Amount = x.Amount }));

        await RunAsync(s => s.Invoking(x => x.UpdateAsync(changed, CancellationToken.None)).Should().ThrowAsync<Exception>().WithMessage("*already paid*"));
        await RunAsync(s => s.Invoking(x => x.UpdateAsync(dropped, CancellationToken.None)).Should().ThrowAsync<Exception>().WithMessage("*already paid*"));
        (await AllRowsAsync()).Should().OnlyContain(x => x.DeletedAt == null && x.Amount == 500m);
    }

    [Fact]
    public async Task UpdateAsync_KeepsAnUnchangedPaidRowAndEditsTheRest()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));
        var stored = (await RunAsync(s => s.FineOneAsync(app.Id, CancellationToken.None)))!;
        var paid = stored.Schedule.First();
        await MarkConsumedAsync(paid.Id);

        var form = UpdateForm(stored, stored.Schedule.Select(x =>
            new UpdateOtherIncomeSchedule { Id = x.Id, Date = x.Date, Amount = x.Id == paid.Id ? x.Amount : 700m }));
        await RunAsync(s => s.UpdateAsync(form, CancellationToken.None));

        var rows = await AllRowsAsync();
        rows.Single(x => x.Id == paid.Id).Amount.Should().Be(500m);
        rows.Where(x => x.Id != paid.Id).Should().OnlyContain(x => x.Amount == 700m && x.DeletedAt == null);
    }

    [Fact]
    public async Task DeleteParent_RemovesItsRowsSoPayrollNoLongerPaysThem()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));

        await RunAsync(s => s.DeleteParent(app.Id, CancellationToken.None));

        (await AllRowsAsync()).Should().BeEmpty("the rows are deleted outright with the application");
        await using var context = _db.NewContext();
        var loaded = await new IncomeAplDtlService(new UnitOfWorkService(context))
            .LoadAsync([_employeeId], Start, Start.AddYears(1), CancellationToken.None);
        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteParent_IsRefusedOnceAnyRowWasPaid()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));
        await MarkConsumedAsync((await AllRowsAsync()).First().Id);

        await RunAsync(s => s.Invoking(x => x.DeleteParent(app.Id, CancellationToken.None)).Should().ThrowAsync<Exception>().WithMessage("*already paid*"));
        (await AllRowsAsync()).Should().OnlyContain(x => x.DeletedAt == null);
    }

    [Fact]
    public async Task AddAsync_PersistsTheAttendanceRuleAndEachRowsPeriod()
    {
        var form = CreateForm(payments: 2);
        form.PayMode = OtherIncomePayMode.PeriodicGated;
        form.FrequencyOfPayment = AllowanceFrequency.Weekly;
        form.AttendanceBasis = OtherIncomeAttendanceBasis.Worked;
        form.MinHours = 8m;
        form.MaxLateMinutes = 0;
        form.ApplicableDays = (int)(OtherIncomeWeekdays.Monday | OtherIncomeWeekdays.Tuesday);
        form.DayTypes = (int)OtherIncomeDayTypes.Regular;
        form.MinQualifyingDays = 2;
        foreach (var row in form.Schedule)
        {
            row.PeriodStart = row.Date.AddDays(-6);
            row.PeriodEnd = row.Date;
        }

        var app = await RunAsync(s => s.AddAsync(form, CancellationToken.None));
        var stored = (await RunAsync(s => s.FineOneAsync(app.Id, CancellationToken.None)))!;

        stored.PayMode.Should().Be(OtherIncomePayMode.PeriodicGated);
        stored.AttendanceBasis.Should().Be(OtherIncomeAttendanceBasis.Worked);
        stored.MinHours.Should().Be(8m);
        stored.MaxLateMinutes.Should().Be(0);
        stored.ApplicableDays.Should().Be(3);
        stored.DayTypes.Should().Be((int)OtherIncomeDayTypes.Regular);
        stored.MinQualifyingDays.Should().Be(2);
        stored.Schedule.Should().OnlyContain(x => x.PeriodStart == x.Date.AddDays(-6) && x.PeriodEnd == x.Date);
    }

    [Fact]
    public async Task AddAsync_AnAttendanceModeWithoutABasis_IsRefused()
    {
        var form = CreateForm();
        form.PayMode = OtherIncomePayMode.PerQualifyingDay;
        form.AttendanceBasis = OtherIncomeAttendanceBasis.None;

        await RunAsync(s => s.Invoking(x => x.AddAsync(form, CancellationToken.None))
            .Should().ThrowAsync<Exception>().WithMessage("*what counts as present*"));
        (await AllRowsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_APeriodicGateThatIsNotWeeklyOrMonthly_IsRefused()
    {
        var form = CreateForm();
        form.PayMode = OtherIncomePayMode.PeriodicGated;
        form.AttendanceBasis = OtherIncomeAttendanceBasis.Worked;
        form.FrequencyOfPayment = AllowanceFrequency.Daily;

        await RunAsync(s => s.Invoking(x => x.AddAsync(form, CancellationToken.None))
            .Should().ThrowAsync<Exception>().WithMessage("*weekly or monthly*"));
    }

    [Fact]
    public async Task AddAsync_AnEndDateBeforeTheStartDate_IsRefused()
    {
        var form = CreateForm();
        form.EndDate = form.StartDate.AddDays(-1);

        await RunAsync(s => s.Invoking(x => x.AddAsync(form, CancellationToken.None))
            .Should().ThrowAsync<Exception>().WithMessage("*End date*"));
    }

    [Fact]
    public async Task AddAsync_AFixedScheduleKeepsTheOriginalBehaviour()
    {
        var app = await RunAsync(s => s.AddAsync(CreateForm(), CancellationToken.None));

        app.PayMode.Should().Be(OtherIncomePayMode.FixedSchedule);
        app.AttendanceBasis.Should().Be(OtherIncomeAttendanceBasis.None);
        app.ApplicableDays.Should().Be((int)OtherIncomeWeekdays.All);
        app.DayTypes.Should().Be((int)OtherIncomeDayTypes.All);
    }
}
