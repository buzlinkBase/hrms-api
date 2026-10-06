using Hrms.Core.Services;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// Day-level payroll tracking behind the per-employee (cutoff + payroll group) payroll run:
/// DailyRecord.PayrollBatchId is stamped when a run pays a day and cleared when the run is
/// deleted, so one employee's days from several DTR batches/clients can be paid across different
/// runs without paying any day twice.
/// </summary>
public class DailyRecordCutoffPayrollTests
{
    private static DailyRecordService BuildService(IUnitOfWorkService uow) =>
        new(uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
            Substitute.For<ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
            new PayrollBatchService(uow), new DTRBatchService(uow),
            new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()));

    // One employee who worked for two clients in the same cutoff: client A's DTR batch is
    // approved (Posted), client B's is still a draft.
    private static (Guid GroupId, Guid EmployeeId, Guid ApprovedDayId, Guid DraftDayId) Seed(HrmsContext seed)
    {
        var groupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = groupId };
        seed.Employees.Add(employee);
        var approved = new DailyRecord
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDate = new DateOnly(2026, 1, 5),
            BatchCode = "DTR-clientA", FullName = "Test Employee", Posted = true,
        };
        var draft = new DailyRecord
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDate = new DateOnly(2026, 1, 6),
            BatchCode = "DTR-clientB", FullName = "Test Employee", Posted = false,
        };
        seed.DailyTimeRecords.Add(approved);
        seed.DailyTimeRecords.Add(draft);
        return (groupId, employee.Id, approved.Id, draft.Id);
    }

    [Fact]
    public async Task GetNotReadyEmployeesAsync_ListsEmployeesWithUnapprovedUnpaidDaysInTheRange()
    {
        using var db = new SqliteHrmsContext();
        Guid groupId, employeeId;
        await using (var seed = db.NewContext())
        {
            (groupId, employeeId, _, _) = Seed(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));

        var result = await service.GetNotReadyEmployeesAsync(
            new CutoffScope(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), groupId), CancellationToken.None);

        var notReady = result.Should().ContainSingle().Subject;
        notReady.EmployeeId.Should().Be(employeeId);
        notReady.UnapprovedDays.Should().Be(1, "only the client B draft day is unapproved");
        notReady.BatchCodes.Should().Equal("DTR-clientB");
    }

    [Fact]
    public async Task GetNotReadyEmployeesAsync_IgnoresOtherPayrollGroupsAndOutOfRangeDays()
    {
        using var db = new SqliteHrmsContext();
        Guid groupId;
        await using (var seed = db.NewContext())
        {
            (groupId, _, _, _) = Seed(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));

        (await service.GetNotReadyEmployeesAsync(
            new CutoffScope(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), Guid.NewGuid()), CancellationToken.None))
            .Should().BeEmpty("the employee belongs to a different payroll group");
        (await service.GetNotReadyEmployeesAsync(
            new CutoffScope(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 15), groupId), CancellationToken.None))
            .Should().BeEmpty("the draft day falls outside this range");
    }

    [Fact]
    public async Task GetNotReadyEmployeesAsync_IncludesEveryGroup_WhenNoPayrollGroupIsGiven()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            (_, employeeId, _, _) = Seed(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));

        var result = await service.GetNotReadyEmployeesAsync(
            new CutoffScope(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), null), CancellationToken.None);

        result.Should().ContainSingle().Which.EmployeeId.Should().Be(employeeId);
    }

    [Fact]
    public async Task GetNotReadyEmployeesAsync_FiltersByTheEmployeesOwnClientAndDepartmentTags()
    {
        using var db = new SqliteHrmsContext();
        Guid groupId;
        await using (var seed = db.NewContext())
        {
            // The seeded employee has no client or department tag at all, so filtering by any
            // client or department excludes them, whatever their DTR rows say.
            (groupId, _, _, _) = Seed(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildService(new UnitOfWorkService(context));
        var range = (From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 1, 15));

        (await service.GetNotReadyEmployeesAsync(
            new CutoffScope(range.From, range.To, groupId, ClientIds: [Guid.NewGuid()]), CancellationToken.None))
            .Should().BeEmpty();
        (await service.GetNotReadyEmployeesAsync(
            new CutoffScope(range.From, range.To, groupId, DepartmentIds: [Guid.NewGuid()]), CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task MarkPaidAsync_StampsTheRun_AndRefusesADayAnotherRunAlreadyPaid()
    {
        using var db = new SqliteHrmsContext();
        Guid approvedDayId;
        await using (var seed = db.NewContext())
        {
            (_, _, approvedDayId, _) = Seed(seed);
            await seed.SaveChangesAsync();
        }
        var firstRun = Guid.NewGuid();

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await BuildService(uow).MarkPaidAsync([approvedDayId], firstRun, CancellationToken.None);
            // MarkPaidAsync only stages the stamp -- the caller (PayrollProcessorService.GenerateAsync)
            // owns the commit, so the test plays that role.
            await uow.CommitChangesAsync("", CancellationToken.None);
        }
        await using (var verify = db.NewContext())
        {
            (await verify.DailyTimeRecords.SingleAsync(x => x.Id == approvedDayId))
                .PayrollBatchId.Should().Be(firstRun);
        }

        await using var again = db.NewContext();
        var act = () => BuildService(new UnitOfWorkService(again))
            .MarkPaidAsync([approvedDayId], Guid.NewGuid(), CancellationToken.None);
        await act.Should().ThrowAsync<ValidationException>("a paid day must never be paid by a second run");
    }

    [Fact]
    public async Task ReleasePaidAsync_ClearsTheRunMarker_AndLeavesTheDayPosted()
    {
        using var db = new SqliteHrmsContext();
        Guid approvedDayId;
        var run = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            (_, _, approvedDayId, _) = Seed(seed);
            await seed.SaveChangesAsync();
            var day = await seed.DailyTimeRecords.SingleAsync(x => x.Id == approvedDayId);
            day.PayrollBatchId = run;
            await seed.SaveChangesAsync();
        }

        await using (var context = db.NewContext())
        {
            var uow = new UnitOfWorkService(context);
            await BuildService(uow).ReleasePaidAsync(run, CancellationToken.None);
            // ReleasePaidAsync only stages the release -- the caller (PayrollBatchLifecycleService.
            // DeleteBatchAsync) owns the commit, so the test plays that role.
            await uow.CommitChangesAsync("", CancellationToken.None);
        }

        await using var verify = db.NewContext();
        var released = await verify.DailyTimeRecords.SingleAsync(x => x.Id == approvedDayId);
        released.PayrollBatchId.Should().BeNull();
        released.Posted.Should().BeTrue("deleting a payroll run must not force the DTR to be re-approved");
    }

    [Fact]
    public async Task GetBatches_FlagsPayrollGeneratedOnlyWhenEveryDayOfTheBatchIsPaid()
    {
        using var db = new SqliteHrmsContext();
        Guid groupId, employeeId;
        await using (var seed = db.NewContext())
        {
            (groupId, employeeId, _, _) = Seed(seed);
            // A second day in client A's batch, so the batch can be partly paid.
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 7),
                BatchCode = "DTR-clientA", Posted = true,
            });
            await seed.SaveChangesAsync();
            var firstDay = await seed.DailyTimeRecords
                .SingleAsync(x => x.EmployeeId == employeeId && x.WorkDate == new DateOnly(2026, 1, 5));
            firstDay.PayrollBatchId = Guid.NewGuid();
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var batches = await BuildService(new UnitOfWorkService(context))
            .GetBatches(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), CancellationToken.None);

        batches.Single(x => x.Code == "DTR-clientA").IsPayrollGenerated
            .Should().BeFalse("only one of the batch's two days has been paid");
    }

    [Fact]
    public async Task GetPayrollReadinessAsync_SplitsDaysIntoApprovedPendingAndPaid()
    {
        using var db = new SqliteHrmsContext();
        Guid groupId, employeeId;
        await using (var seed = db.NewContext())
        {
            (groupId, employeeId, _, _) = Seed(seed);
            // An approved day already paid by a run, and one outside the range.
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 7),
                BatchCode = "DTR-clientA", Posted = true, PayrollBatchId = Guid.NewGuid(),
            });
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 20),
                BatchCode = "DTR-clientA", Posted = true,
            });
            // Another group's employee, filtered out by payrollGroupId.
            var otherGroupId = Guid.NewGuid();
            seed.PayrollGroups.Add(new PayrollGroup { Id = otherGroupId });
            var other = new Employee { Id = Guid.NewGuid(), PayrollGroupId = otherGroupId };
            seed.Employees.Add(other);
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = other.Id, WorkDate = new DateOnly(2026, 1, 5),
                BatchCode = "DTR-other", Posted = true,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var result = await BuildService(new UnitOfWorkService(context))
            .GetPayrollReadinessAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), groupId, CancellationToken.None);

        var row = result.Should().ContainSingle().Subject;
        row.EmployeeId.Should().Be(employeeId);
        row.PayrollGroupId.Should().Be(groupId);
        row.ApprovedDays.Should().Be(1, "only the unpaid client A day in range is approved and payable");
        row.PendingDays.Should().Be(1);
        row.PaidDays.Should().Be(1);
        row.PendingBatchCodes.Should().Equal("DTR-clientB");
        row.SavedDates.Should().Equal(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 6), new DateOnly(2026, 1, 7));
    }
}
