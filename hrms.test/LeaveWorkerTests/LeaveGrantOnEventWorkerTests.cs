using hrms.test.LeaveWorkerTests.TestSupport;
using NSubstitute;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// LeaveGrantOnEventWorker — fires when a leave application is filed; grants LeaveCredits
/// immediately for PerEvent leave types (Maternity, Paternity, etc.) so long as the type isn't
/// Government-funded (SSS pays those directly) and no credit record already exists for the
/// employee/leave/year.
/// </summary>
public class LeaveGrantOnEventWorkerTests : LeaveWorkerTestBase
{
    private static (LeaveGrantOnEventWorker Worker, IRepository Repo) BuildWorker()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        var worker = new LeaveGrantOnEventWorker(uow, CreateLogger<LeaveGrantOnEventWorker>());
        return (worker, repo);
    }

    [Fact]
    public async Task PerEventCompanyFundedLeave_NoExistingCredits_GrantsCreditsAndLedgerEntry()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.PerEvent, credits: 7, paySource: PaySource.Company);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var employeeId = NewId();
        var applicationId = NewId();
        var context = CreateConsumeContext(new LeaveApplicationCreated(applicationId, employeeId, leave.Id));

        await worker.Consume(context);

        addedCredits.Should().ContainSingle();
        addedCredits[0].EmployeeId.Should().Be(employeeId);
        addedCredits[0].LeaveId.Should().Be(leave.Id);
        addedCredits[0].Granted.Should().Be(7m);
        addedCredits[0].Balance.Should().Be(7m);

        addedLedgers.Should().ContainSingle();
        addedLedgers[0].EntryType.Should().Be(LedgerEntryType.Grant);
        addedLedgers[0].ReferenceApplicationId.Should().Be(applicationId);
        addedLedgers[0].LeaveCreditsId.Should().Be(addedCredits[0].Id);
    }

    [Fact]
    public async Task LeaveNotPerEvent_DoesNothing()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly);
        SeedFind(repo, leave);
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new LeaveApplicationCreated(NewId(), NewId(), leave.Id));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task GovernmentFundedLeave_DoesNothing()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.PerEvent, paySource: PaySource.Government);
        SeedFind(repo, leave);
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new LeaveApplicationCreated(NewId(), NewId(), leave.Id));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveNotFound_DoesNothing()
    {
        var (worker, repo) = BuildWorker();
        SeedFind(repo, Array.Empty<Leave>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new LeaveApplicationCreated(NewId(), NewId(), NewId()));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task CreditsAlreadyExistForThisYear_SkipsAsIdempotencyGuard()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.PerEvent);
        var employeeId = NewId();
        SeedFind(repo, leave);
        SeedFind(repo, BuildCredits(leave, employeeId, DateTime.UtcNow.Year, granted: 5));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new LeaveApplicationCreated(NewId(), employeeId, leave.Id));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }
}
