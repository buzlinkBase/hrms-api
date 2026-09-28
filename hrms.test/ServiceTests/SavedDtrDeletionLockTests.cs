using Hrms.Core.Services.Applications;
using Hrms.Core.Services.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;
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
/// An approved application can't be deleted once any saved DTR batch (draft or later) includes
/// its days -- that DTR, and any payroll built on it, would keep paying for something that no
/// longer exists. Pending/declined applications, and approved ones without a saved DTR, still
/// delete. Real SQLite rows, real services.
/// </summary>
public class SavedDtrDeletionLockTests
{
    private const string Batch = "DTR-0012";
    private static readonly DateOnly Sep3 = new(2026, 9, 3);
    private static readonly DateOnly Sep4 = new(2026, 9, 4);

    private static async Task<Employee> SeedEmployeeAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext();
        var payrollGroupId = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = payrollGroupId });
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = payrollGroupId };
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    private static async Task SeedDtrAsync(SqliteHrmsContext db, Guid employeeId, DateOnly date, string? batchCode = Batch)
    {
        await using var seed = db.NewContext();
        seed.DailyTimeRecords.Add(new DailyRecord { Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = date, BatchCode = batchCode });
        await seed.SaveChangesAsync();
    }

    private static async Task RunAsync(SqliteHrmsContext db, Func<IUnitOfWorkService, Task> action)
    {
        await using var context = db.NewContext();
        await action(new UnitOfWorkService(context));
    }

    private static ApprovalEngineService Engine(IUnitOfWorkService uow) =>
        new(uow, Substitute.For<IPublishEndpoint>());

    // ---- policy ----

    private static async Task CheckAsync(SqliteHrmsContext db, ApprovalStatus status, Guid employeeId, DateOnly from, DateOnly to)
    {
        await using var context = db.NewContext();
        await SavedDtrDeletionPolicy.EnsureDeletableAsync(context.DailyTimeRecords, status, employeeId, from, to, "leave", CancellationToken.None);
    }

    [Fact]
    public async Task Policy_Approved_WithSavedDtrInRange_Throws_NamingTheBatch()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedDtrAsync(db, employee.Id, Sep4);

        var act = () => CheckAsync(db, ApprovalStatus.Approved, employee.Id, Sep3, Sep4);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain(Batch).And.Contain("approved leave");
    }

    [Fact]
    public async Task Policy_Approved_WithDtrOutsideTheRange_IsAllowed()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedDtrAsync(db, employee.Id, new DateOnly(2026, 9, 10));

        await CheckAsync(db, ApprovalStatus.Approved, employee.Id, Sep3, Sep4);
    }

    [Fact]
    public async Task Policy_Approved_WithUnsavedDtrRow_IsAllowed()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedDtrAsync(db, employee.Id, Sep3, batchCode: null);

        await CheckAsync(db, ApprovalStatus.Approved, employee.Id, Sep3, Sep4);
    }

    [Fact]
    public async Task Policy_AnotherEmployeesDtr_DoesNotBlock()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var other = await SeedEmployeeAsync(db);
        await SeedDtrAsync(db, other.Id, Sep3);

        await CheckAsync(db, ApprovalStatus.Approved, employee.Id, Sep3, Sep4);
    }

    [Theory]
    [InlineData(ApprovalStatus.ForApproval)]
    [InlineData(ApprovalStatus.Declined)]
    [InlineData(ApprovalStatus.Cancelled)]
    public async Task Policy_NotApproved_IsAllowed_EvenWithASavedDtr(ApprovalStatus status)
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        await SeedDtrAsync(db, employee.Id, Sep3);

        await CheckAsync(db, status, employee.Id, Sep3, Sep4);
    }

    // ---- services ----

    private static async Task<Guid> SeedOvertimeAsync(SqliteHrmsContext db, Guid employeeId)
    {
        await using var seed = db.NewContext();
        var employee = await seed.Employees.SingleAsync(x => x.Id == employeeId);
        var app = new OverTimeApplication { Id = Guid.NewGuid(), EmployeeId = employeeId, Employee = employee, OTDate = Sep3, ApprovalStatus = ApprovalStatus.Approved };
        seed.OTApplications.Add(app);
        await seed.SaveChangesAsync();
        return app.Id;
    }

    private static async Task<Guid> SeedOfficialBusinessAsync(SqliteHrmsContext db, Guid employeeId)
    {
        await using var seed = db.NewContext();
        var app = new TravelOrderApplication { Id = Guid.NewGuid(), EmployeeId = employeeId, StartDate = Sep3, EndDate = Sep4, ApprovalStatus = ApprovalStatus.Approved };
        seed.TravelOrderApplications.Add(app);
        await seed.SaveChangesAsync();
        return app.Id;
    }

    private static async Task<Guid> SeedPassSlipAsync(SqliteHrmsContext db, Guid employeeId)
    {
        await using var seed = db.NewContext();
        var app = new PassSlipApplication { Id = Guid.NewGuid(), EmployeeId = employeeId, ApplicationDate = Sep3, ApprovalStatus = ApprovalStatus.Approved };
        seed.PassSlipApplications.Add(app);
        await seed.SaveChangesAsync();
        return app.Id;
    }

    private static async Task<bool> ExistsAsync<T>(SqliteHrmsContext db, Guid id) where T : class
    {
        await using var verify = db.NewContext();
        return await verify.Set<T>().AsNoTracking().AnyAsync(x => EF.Property<Guid>(x, "Id") == id);
    }

    [Fact]
    public async Task Overtime_ApprovedWithSavedDtr_IsNotDeleted()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var id = await SeedOvertimeAsync(db, employee.Id);
        await SeedDtrAsync(db, employee.Id, Sep3);

        var act = () => RunAsync(db, uow => new OvertimeApplicationService(uow, Engine(uow)).Delete(id, CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*approved overtime*");
        (await ExistsAsync<OverTimeApplication>(db, id)).Should().BeTrue();
    }

    [Fact]
    public async Task Overtime_ApprovedWithoutDtr_IsDeleted()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var id = await SeedOvertimeAsync(db, employee.Id);

        await RunAsync(db, uow => new OvertimeApplicationService(uow, Engine(uow)).Delete(id, CancellationToken.None));

        (await ExistsAsync<OverTimeApplication>(db, id)).Should().BeFalse();
    }

    [Fact]
    public async Task OfficialBusiness_ApprovedWithSavedDtrOnItsLastDay_IsNotDeleted()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var id = await SeedOfficialBusinessAsync(db, employee.Id);
        await SeedDtrAsync(db, employee.Id, Sep4);

        var act = () => RunAsync(db, uow => new TravelOrderApplicationService(uow, Engine(uow)).Delete(id, CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*approved official business*");
        (await ExistsAsync<TravelOrderApplication>(db, id)).Should().BeTrue();
    }

    [Fact]
    public async Task OfficialBusiness_ApprovedWithoutDtr_IsDeleted()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var id = await SeedOfficialBusinessAsync(db, employee.Id);

        await RunAsync(db, uow => new TravelOrderApplicationService(uow, Engine(uow)).Delete(id, CancellationToken.None));

        (await ExistsAsync<TravelOrderApplication>(db, id)).Should().BeFalse();
    }

    [Fact]
    public async Task PassSlip_ApprovedWithSavedDtr_IsNotDeleted()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var id = await SeedPassSlipAsync(db, employee.Id);
        await SeedDtrAsync(db, employee.Id, Sep3);

        var act = () => RunAsync(db, uow => new PassSlipApplicationService(uow, null!, Engine(uow)).Delete(id, CancellationToken.None));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*approved pass slip*");
        (await ExistsAsync<PassSlipApplication>(db, id)).Should().BeTrue();
    }

    [Fact]
    public async Task PassSlip_ApprovedWithoutDtr_IsDeleted()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var id = await SeedPassSlipAsync(db, employee.Id);

        await RunAsync(db, uow => new PassSlipApplicationService(uow, null!, Engine(uow)).Delete(id, CancellationToken.None));

        (await ExistsAsync<PassSlipApplication>(db, id)).Should().BeFalse();
    }

    // ---- leave ----

    private sealed record SeededLeave(Guid ApplicationId, Guid CreditsId);

    // An approved 2-day leave holding its 2-day approval-time reservation.
    private static async Task<SeededLeave> SeedLeaveAsync(SqliteHrmsContext db, Guid employeeId)
    {
        await using var seed = db.NewContext();
        var leave = new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave" };
        seed.Leaves.Add(leave);
        var credits = new LeaveCredits
        {
            Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveId = leave.Id, PeriodYear = 2026,
            FromDate = new DateTime(2026, 1, 1), ToDate = new DateTime(2026, 12, 31),
            Granted = 10m, Balance = 10m, Reserved = 2m,
        };
        seed.LeaveCredits.Add(credits);
        var app = new LeaveApplication
        {
            Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveId = leave.Id, Leave = null!,
            DurationType = DurationType.MultiDay, LeaveDateFrom = Sep3, LeaveDateTo = Sep4,
            ApprovalStatus = ApprovalStatus.Approved,
        };
        seed.leaveApplications.Add(app);
        seed.LeaveLedgers.Add(new LeaveLedger
        {
            Id = Guid.NewGuid(), EmployeeId = employeeId, LeaveId = leave.Id, LeaveCreditsId = credits.Id,
            EntryType = LedgerEntryType.Reserved, EntryDate = Sep3, Less = 2m, Balance = 10m,
            ReferenceApplicationId = app.Id, Particulars = "Leave approved",
        });
        await seed.SaveChangesAsync();
        return new SeededLeave(app.Id, credits.Id);
    }

    private static async Task DeleteLeaveAsync(SqliteHrmsContext db, Guid id) =>
        await RunAsync(db, async uow =>
        {
            var reconciliation = new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>());
            var dtrService = new DailyRecordService(
                uow, new TypeAdapterConfig(), Substitute.For<IMapper>(), Substitute.For<ILogger<DailyRecordService>>(),
                reconciliation, new PayrollBatchService(uow), new DTRBatchService(uow), Engine(uow));
            var service = new LeaveApplicationService(
                uow, new TypeAdapterConfig(), Substitute.For<IMapper>(), Substitute.For<IPublishEndpoint>(),
                Substitute.For<ILogger<LeaveApplicationService>>(), dtrService, Engine(uow), reconciliation);
            await service.Delete(id, CancellationToken.None);
            await service.CommitChangesAsync(CancellationToken.None);
        });

    [Fact]
    public async Task Leave_ApprovedWithSavedDtr_IsNotDeleted_AndKeepsItsReservation()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var s = await SeedLeaveAsync(db, employee.Id);
        await SeedDtrAsync(db, employee.Id, Sep3);

        var act = () => DeleteLeaveAsync(db, s.ApplicationId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*approved leave*");
        await using var verify = db.NewContext();
        (await verify.leaveApplications.AnyAsync(x => x.Id == s.ApplicationId)).Should().BeTrue();
        (await verify.LeaveCredits.SingleAsync(x => x.Id == s.CreditsId)).Reserved.Should().Be(2m);
    }

    [Fact]
    public async Task Leave_ApprovedWithoutDtr_IsDeleted_AndGivesItsCreditsBack()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedEmployeeAsync(db);
        var s = await SeedLeaveAsync(db, employee.Id);

        await DeleteLeaveAsync(db, s.ApplicationId);

        await using var verify = db.NewContext();
        (await verify.leaveApplications.AnyAsync(x => x.Id == s.ApplicationId)).Should().BeFalse();
        var credits = await verify.LeaveCredits.SingleAsync(x => x.Id == s.CreditsId);
        credits.Reserved.Should().Be(0m, "a deleted leave holds nothing");
        credits.Balance.Should().Be(10m);
    }
}
