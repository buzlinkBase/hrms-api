using hrms.test.LeaveWorkerTests.TestSupport;
using Hrms.Domain.Entities.EmployeeEntities;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// LeavePeriodGrantWorker — fires on the fiscal year's first day, creating a LeaveCredits
/// record (+ Grant ledger entry) for every active employee against every lump-sum
/// (AccrualBasis.None) leave type they've served long enough for, skipping anyone who already
/// has a credit record for that year.
/// </summary>
public class LeavePeriodGrantWorkerTests : LeaveWorkerTestBase
{
    private static (LeavePeriodGrantWorker Worker, IRepository Repo) BuildWorker()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        var worker = new LeavePeriodGrantWorker(uow, CreateLogger<LeavePeriodGrantWorker>());
        return (worker, repo);
    }

    [Fact]
    public async Task EligibleActiveEmployee_NoExistingCredit_GrantsCreditsAndLedgerEntry()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, credits: 15, minServiceMonths: 0);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().ContainSingle();
        addedCredits[0].EmployeeId.Should().Be(employee.Id);
        addedCredits[0].LeaveId.Should().Be(leave.Id);
        addedCredits[0].Granted.Should().Be(15m);
        addedCredits[0].PeriodYear.Should().Be(2026);

        addedLedgers.Should().ContainSingle();
        addedLedgers[0].EntryType.Should().Be(LedgerEntryType.Grant);
        addedLedgers[0].LeaveCreditsId.Should().Be(addedCredits[0].Id);
    }

    [Fact]
    public async Task NoLumpSumLeaveTypesConfigured_DoesNothing()
    {
        var (worker, repo) = BuildWorker();
        SeedFind(repo, Array.Empty<Leave>());
        var employee = BuildEmployee();
        SeedFind(repo, employee);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task EmployeeBelowMinServiceMonths_IsSkipped()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, minServiceMonths: 12);
        var newHire = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2025, 12, 1)); // ~1 month tenure by Jan 2026
        SeedFind(repo, leave);
        SeedFind(repo, newHire);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task InactiveEmploymentStatus_IsExcludedFromTheEmployeeQuery()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None);
        var resigned = BuildEmployee(EmploymentStatus.Resigned, hireDate: new DateOnly(2015, 1, 1));
        SeedFind(repo, leave);
        SeedFind(repo, resigned); // seeded but the worker's own predicate filters it out
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task EmployeeAlreadyHasACreditForThisYear_IsSkipped()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        SeedFind(repo, BuildCredits(leave, employee.Id, 2026, granted: 15));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleLeaveTypesAndEmployees_GrantsTheFullCrossProductMinusExisting()
    {
        var (worker, repo) = BuildWorker();
        var leaveA = BuildLeave(AccrualBasis.None, credits: 5, description: "Vacation");
        var leaveB = BuildLeave(AccrualBasis.None, credits: 3, description: "Sick");
        var empX = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        var empY = BuildEmployee(EmploymentStatus.Probationary, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, leaveA, leaveB);
        SeedFind(repo, empX, empY);
        // empY already has a credit for leaveA this year -> that one pairing should be skipped
        SeedFind(repo, BuildCredits(leaveA, empY.Id, 2026, granted: 5));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().HaveCount(3); // (empX,leaveA) (empX,leaveB) (empY,leaveB) -- not (empY,leaveA)
        addedCredits.Should().NotContain(c => c.EmployeeId == empY.Id && c.LeaveId == leaveA.Id);
    }
}
