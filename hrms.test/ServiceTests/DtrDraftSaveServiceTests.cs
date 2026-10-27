using Hrms.Core.Messaging;
using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Onepunch.Common.Lib;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>
/// Calculate DTR "Save Draft" by reference (DtrDraftSaveService + SaveDtrDraftWorker) and the
/// per-employee summary the review screen now loads instead of every day row. A company with
/// ~2,000 employees is tens of thousands of rows -- far past what the browser can upload in one
/// request -- so the server recomputes and saves them itself.
/// </summary>
public class DtrDraftSaveServiceTests
{
    // The empty tenant: rows the service saves aren't stamped with a company here (the production
    // context does that on SaveChanges), so every context in this file reads the same, unstamped scope.
    private static readonly Guid CompanyId = Guid.Empty;
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 9, 16);
    private static readonly DateOnly To = new(2026, 9, 30);

    private sealed class FakeRows(List<DTRDetailModel> rows) : IDtrRowSource
    {
        public Task<List<DTRDetailModel>> ComputeAsync(DTRRequestPayload payload, CancellationToken token) =>
            Task.FromResult(rows.Select(Clone).ToList());

        // Each call hands out fresh rows, like recomputing does.
        private static DTRDetailModel Clone(DTRDetailModel r) => new()
        {
            EmployeeId = r.EmployeeId, FullName = r.FullName, WorkDate = r.WorkDate, PayrollGroupId = r.PayrollGroupId,
            RegularNetHours = r.RegularNetHours, StartTime = r.StartTime, EndTime = r.EndTime,
        };
    }

    private static DtrDraftSaveService BuildSaver(IUnitOfWorkService uow, IDtrRowSource rows, IPublishEndpoint publisher)
    {
        _ = TestMapping.GlobalSettings;
        var approvals = new ApprovalEngineService(uow, publisher);
        var records = new DailyRecordService(
            uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
            Substitute.For<ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
            new PayrollBatchService(uow), new DTRBatchService(uow), approvals);
        return new DtrDraftSaveService(records, rows, approvals, publisher, new Mapper(TestMapping.GlobalSettings));
    }

    private static async Task<List<Employee>> SeedEmployeesAsync(SqliteHrmsContext db, int count)
    {
        await using var seed = db.NewContext(CompanyId);
        seed.PayrollGroups.Add(new PayrollGroup { Id = Group, TenantId = CompanyId });
        var employees = Enumerable.Range(0, count).Select(i => new Employee
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, PayrollGroupId = Group,
            FirstName = $"Emp{i:D2}", LastName = "Test", Email = $"emp{i}@acme.com",
        }).ToList();
        seed.Employees.AddRange(employees);
        await seed.SaveChangesAsync();
        return employees;
    }

    private static DTRDetailModel Row(Employee e, DateOnly day) => new()
    {
        EmployeeId = e.Id, FullName = e.FirstName, WorkDate = day, PayrollGroupId = Group, RegularNetHours = 8,
    };

    private static SaveDtrDraftRequested Request(Guid generatedBy, params Employee[] employees) => new()
    {
        FromDate = From, ToDate = To, PayrollGroupId = Group,
        EmployeeIds = employees.Select(x => x.Id).ToList(),
        PostingDescription = "Sep 16-30", UserId = Guid.NewGuid(), GeneratedByEmployeeId = generatedBy,
    };

    private static async Task<DtrDraftSaveResult> SaveAsync(
        SqliteHrmsContext db, IDtrRowSource rows, IPublishEndpoint publisher, SaveDtrDraftRequested request)
    {
        await using var context = db.NewContext(CompanyId);
        return await BuildSaver(new UnitOfWorkService(context), rows, publisher).SaveAsync(request, CancellationToken.None);
    }

    // ---- the per-employee summary ----

    [Fact]
    public void Summary_TotalsEachEmployeesDaysAndFlagsMissingPunches()
    {
        var ana = Guid.NewGuid();
        var ben = Guid.NewGuid();
        var rows = new List<DTRDetailModel>
        {
            new() { EmployeeId = ben, FullName = "Ben", WorkDate = From, RegularNetHours = 8, RegularOTHours = 1, LateMinutes = 5,
                StartTime = From.ToDateTime(new TimeOnly(8, 0)), EndTime = From.ToDateTime(new TimeOnly(17, 0)) },
            new() { EmployeeId = ana, FullName = "Ana", WorkDate = From, RegularNetHours = 8, RegularNDHours = 2, RestDayOTHours = 1,
                StartTime = From.ToDateTime(new TimeOnly(8, 0)), EndTime = null },
            new() { EmployeeId = ana, FullName = "Ana", WorkDate = From.AddDays(1), AbsentCount = 1, PaidLeaveHours = 8, UnpaidLeaveHours = 2, UTMinutes = 15 },
            new() { EmployeeId = ana, FullName = "Ana", WorkDate = From.AddDays(1), RegularNetHours = 0 },
        };

        var summary = DtrSummaryBuilder.Build(rows);

        summary.Select(x => x.FullName).Should().Equal("Ana", "Ben");
        var a = summary[0];
        a.WorkDates.Should().Equal(From, From.AddDays(1));
        a.RegularHours.Should().Be(8);
        a.NdHours.Should().Be(2);
        a.OtHours.Should().Be(1);
        a.Absences.Should().Be(1);
        a.LeaveHours.Should().Be(10);
        a.UtMinutes.Should().Be(15);
        a.MissingPunchDays.Should().Be(1, "only the first day has a time-in without a time-out");
        summary[1].LateMinutes.Should().Be(5);
        summary[1].MissingPunchDays.Should().Be(0);
    }

    // ---- saving by reference ----

    [Fact]
    public async Task Save_PersistsOnlyTheTickedEmployeesAsAnUnpostedDraftWithAnApproval()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 3);
        var rows = new FakeRows(employees.SelectMany(e => new[] { Row(e, From), Row(e, From.AddDays(1)) }).ToList());
        var publisher = Substitute.For<IPublishEndpoint>();

        var result = await SaveAsync(db, rows, publisher, Request(employees[0].Id, employees[0], employees[1]));

        result.EmployeeCount.Should().Be(2);
        result.RowCount.Should().Be(4);
        await using var verify = db.NewContext(CompanyId);
        var saved = await verify.DailyTimeRecords.Where(x => x.BatchCode == result.BatchCode).ToListAsync();
        saved.Should().HaveCount(4);
        saved.Select(x => x.EmployeeId).Distinct().Should().BeEquivalentTo(new[] { employees[0].Id, employees[1].Id });
        saved.Should().OnlyContain(x => !x.Posted && x.PostingDescription == "Sep 16-30" && x.BatchCode == result.BatchCode);
        var batch = await verify.DTRBatches.SingleAsync(x => x.BatchCode == result.BatchCode);
        batch.ApprovalStatus.Should().Be(ApprovalStatus.ForApproval);
        batch.GeneratedByEmployeeId.Should().Be(employees[0].Id);
        (await verify.ApprovalInstances.AnyAsync(x => x.ApplicationId == batch.Id && x.ApplicationType == ApprovalApplicationType.Dtr))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Save_DoesNotTellTheRequesterTheirOwnDraftWasSaved()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 1);
        var rows = new FakeRows([Row(employees[0], From)]);
        var publisher = Substitute.For<IPublishEndpoint>();

        await SaveAsync(db, rows, publisher, Request(employees[0].Id, employees[0]));

        // The poster started the save, so the success notice isn't news to them.
        await publisher.DidNotReceive().Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.StatusLabel == DtrDraftSaveService.DraftSavedStatusLabel && m.RecipientEmail == "emp0@acme.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_SkipsDaysThatAlreadyHaveADtrInsteadOfFailing()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 1);
        await using (var seed = db.NewContext(CompanyId))
        {
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employees[0].Id, WorkDate = From, BatchCode = "OLD",
            });
            await seed.SaveChangesAsync();
        }
        var rows = new FakeRows([Row(employees[0], From), Row(employees[0], From.AddDays(1))]);

        var result = await SaveAsync(db, rows, Substitute.For<IPublishEndpoint>(), Request(employees[0].Id, employees[0]));

        result.RowCount.Should().Be(1, "the day that was already saved is left alone");
        await using var verify = db.NewContext(CompanyId);
        (await verify.DailyTimeRecords.Where(x => x.BatchCode == result.BatchCode).SingleAsync()).WorkDate.Should().Be(From.AddDays(1));
        (await verify.DailyTimeRecords.CountAsync(x => x.BatchCode == "OLD")).Should().Be(1);
    }

    [Fact]
    public async Task Save_WithEverythingAlreadySaved_CreatesNoBatchAndSaysSo()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 1);
        await using (var seed = db.NewContext(CompanyId))
        {
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employees[0].Id, WorkDate = From, BatchCode = "OLD",
            });
            await seed.SaveChangesAsync();
        }
        var publisher = Substitute.For<IPublishEndpoint>();

        var result = await SaveAsync(db, new FakeRows([Row(employees[0], From)]), publisher, Request(employees[0].Id, employees[0]));

        result.NothingToSave.Should().BeTrue();
        await using var verify = db.NewContext(CompanyId);
        (await verify.DTRBatches.AnyAsync()).Should().BeFalse();
        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m => m.StatusLabel == DtrDraftSaveService.NothingToSaveStatusLabel),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_ChunksALargeCutoffAndStillSavesEveryRow()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 3);
        var perEmployee = DailyRecordService.SaveChunkSize * 2 / 3 + 100; // 3 employees -> more than two chunks
        var start = new DateOnly(2020, 1, 1);
        var rows = new FakeRows(employees
            .SelectMany(e => Enumerable.Range(0, perEmployee).Select(d => Row(e, start.AddDays(d))))
            .ToList());
        var request = Request(employees[0].Id, employees.ToArray()) with { FromDate = start, ToDate = start.AddDays(perEmployee) };

        var result = await SaveAsync(db, rows, Substitute.For<IPublishEndpoint>(), request);

        result.RowCount.Should().Be(perEmployee * 3);
        result.RowCount.Should().BeGreaterThan(DailyRecordService.SaveChunkSize * 2);
        await using var verify = db.NewContext(CompanyId);
        (await verify.DailyTimeRecords.CountAsync(x => x.BatchCode == result.BatchCode)).Should().Be(perEmployee * 3);
    }

    [Fact]
    public async Task Save_IsAllOrNothing_WhenSomethingFailsAfterTheRowsAreWritten()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 1);
        var records = Enumerable.Range(0, DailyRecordService.SaveChunkSize + 50).Select(d => new DailyRecord
        {
            Id = Guid.NewGuid(), TenantId = CompanyId, EmployeeId = employees[0].Id, PayrollGroupId = Group,
            WorkDate = From.AddDays(d), BatchCode = "ATOMIC",
        }).ToList();

        await using (var context = db.NewContext(CompanyId))
        {
            var publisher = Substitute.For<IPublishEndpoint>();
            var records_ = BuildSaverRecords(new UnitOfWorkService(context), publisher);
            var act = () => records_.SaveDraftAsync(records, "ATOMIC", From, From.AddDays(records.Count), Group, employees[0].Id,
                CancellationToken.None, beforeCommit: (_, _) => throw new InvalidOperationException("boom"));
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        await using var verify = db.NewContext(CompanyId);
        (await verify.DailyTimeRecords.CountAsync(x => x.BatchCode == "ATOMIC")).Should().Be(0, "no chunk survives a failed save");
        (await verify.DTRBatches.AnyAsync(x => x.BatchCode == "ATOMIC")).Should().BeFalse();
    }

    private static DailyRecordService BuildSaverRecords(IUnitOfWorkService uow, IPublishEndpoint publisher) => new(
        uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
        Substitute.For<ILogger<DailyRecordService>>(),
        new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
        new PayrollBatchService(uow), new DTRBatchService(uow), new ApprovalEngineService(uow, publisher));

    // ---- the worker ----

    private static SaveDtrDraftWorker BuildWorker(DtrDraftSaveService saver, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        var tenant = Substitute.For<ITenantProvider>();
        tenant.TenantId.Returns(CompanyId);
        services.AddScoped(_ => tenant);
        services.AddScoped<TenantConnectionStringInfo>();
        services.AddScoped(_ => saver);
        configure?.Invoke(services);
        var provider = services.BuildServiceProvider();
        return new SaveDtrDraftWorker(saver, provider.GetRequiredService<IServiceScopeFactory>(), tenant, new TenantConnectionStringInfo());
    }

    [Fact]
    public async Task Worker_ASaveThatFails_TellsTheRequesterAndDoesNotThrow()
    {
        using var db = new SqliteHrmsContext();
        var employees = await SeedEmployeesAsync(db, 1);
        var publisher = Substitute.For<IPublishEndpoint>();
        await using var context = db.NewContext(CompanyId);
        var failing = Substitute.For<IDtrRowSource>();
        failing.ComputeAsync(Arg.Any<DTRRequestPayload>(), Arg.Any<CancellationToken>())
            .Returns<Task<List<DTRDetailModel>>>(_ => throw new ValidationException("DTR already exists for 1 employee-date(s)."));
        var saver = BuildSaver(new UnitOfWorkService(context), failing, publisher);
        var worker = BuildWorker(saver);
        var consume = Substitute.For<ConsumeContext<SaveDtrDraftRequested>>();
        consume.Message.Returns(Request(employees[0].Id, employees[0]));
        consume.CancellationToken.Returns(CancellationToken.None);

        await worker.Consume(consume);

        await publisher.Received(1).Publish(
            Arg.Is<ApprovalNotificationRequested>(m =>
                m.StatusLabel == DtrDraftSaveService.SaveFailedStatusLabel &&
                m.Note != null && m.Note.Contains("already exists")),
            Arg.Any<CancellationToken>());
    }
}
