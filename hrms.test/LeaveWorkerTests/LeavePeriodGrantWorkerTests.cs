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
        // Real DailyRecordService — only ever called when a Leave uses EligibilityBasis.
        // PresentDays (see LeavePeriodGrantWorker.Consume), so the tenure-based tests below
        // never reach it, and the PresentDays-specific tests seed DailyRecord data for it.
        var worker = new LeavePeriodGrantWorker(uow, CreateLogger<LeavePeriodGrantWorker>(), BuildDailyRecordService(uow));
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
    public async Task EmployeeAlreadyGrantedThisYear_IsSkipped()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        var existing = BuildCredits(leave, employee.Id, 2026, granted: 15);
        SeedFind(repo, existing);
        SeedFind(repo, Ledger(existing, LedgerEntryType.Grant));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
        addedLedgers.Should().BeEmpty();
        existing.Granted.Should().Be(15m);
    }

    // A record HR set by hand (manual Adjustment) is theirs -- the grant must not stack on it.
    [Fact]
    public async Task RecordSetByHrThisYear_IsLeftAlone()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, credits: 5);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        var existing = BuildCredits(leave, employee.Id, 2026, granted: 12);
        SeedFind(repo, existing);
        SeedFind(repo, Ledger(existing, LedgerEntryType.Adjustment));
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        await worker.Consume(CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1)));

        addedLedgers.Should().BeEmpty();
        existing.Granted.Should().Be(12m);
    }

    // Regression: carry-over runs first and creates the new year's record for anyone with
    // unused days -- the grant used to see "a record exists" and skip them, so exactly the
    // people who carried days over never got their new year's credits.
    [Fact]
    public async Task RecordOpenedByCarryOver_GetsTheGrantAddedToIt()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, credits: 5);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        var carried = BuildCredits(leave, employee.Id, 2026, granted: 3);
        SeedFind(repo, carried);
        SeedFind(repo, Ledger(carried, LedgerEntryType.CarryOver));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        await worker.Consume(CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1)));

        addedCredits.Should().BeEmpty("the grant goes into the existing record");
        carried.Granted.Should().Be(8m);
        carried.Balance.Should().Be(8m);
        var grant = addedLedgers.Should().ContainSingle().Subject;
        grant.EntryType.Should().Be(LedgerEntryType.Grant);
        grant.Amount.Should().Be(5m);
        grant.Balance.Should().Be(8m);
        grant.LeaveCreditsId.Should().Be(carried.Id);
    }

    private static LeaveLedger Ledger(LeaveCredits credits, LedgerEntryType type) => new()
    {
        Id = NewId(),
        EmployeeId = credits.EmployeeId,
        LeaveId = credits.LeaveId,
        LeaveCreditsId = credits.Id,
        EntryType = type,
        EntryDate = new DateOnly(credits.PeriodYear, 1, 1),
        Amount = credits.Granted,
        Balance = credits.Balance,
    };

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
        // empY was already granted leaveA this year -> that one pairing should be skipped
        var alreadyGranted = BuildCredits(leaveA, empY.Id, 2026, granted: 5);
        SeedFind(repo, alreadyGranted);
        SeedFind(repo, Ledger(alreadyGranted, LedgerEntryType.Grant));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().HaveCount(3); // (empX,leaveA) (empX,leaveB) (empY,leaveB) -- not (empY,leaveA)
        addedCredits.Should().NotContain(c => c.EmployeeId == empY.Id && c.LeaveId == leaveA.Id);
    }

    private static DailyRecord BuildPostedDay(Guid employeeId, DateOnly workDate, WorkType workType) => new()
    {
        Id = NewId(),
        EmployeeId = employeeId,
        WorkDate = workDate,
        WorkTypeEnum = workType,
        Posted = true,
    };

    [Fact]
    public async Task PresentDaysBasis_EnoughPresentDays_Grants()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, credits: 10,
            eligibilityBasis: LeaveEligibilityBasis.PresentDays, minPresentDays: 2);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2025, 12, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        // Two worked days plus one Absent day -- Absent must not count toward the 2-day minimum.
        SeedFindAll(repo,
            BuildPostedDay(employee.Id, new DateOnly(2025, 12, 15), WorkType.RegularWorkDay),
            BuildPostedDay(employee.Id, new DateOnly(2025, 12, 22), WorkType.RestDay),
            BuildPostedDay(employee.Id, new DateOnly(2025, 12, 29), WorkType.Absent));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().ContainSingle();
        addedCredits[0].EmployeeId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task PresentDaysBasis_NotEnoughPresentDays_IsSkipped()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None,
            eligibilityBasis: LeaveEligibilityBasis.PresentDays, minPresentDays: 5);
        var employee = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2025, 12, 1));
        SeedFind(repo, leave);
        SeedFind(repo, employee);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        SeedFindAll(repo, BuildPostedDay(employee.Id, new DateOnly(2025, 12, 15), WorkType.RegularWorkDay));
        var addedCredits = CaptureAdds<LeaveCredits>(repo);

        var context = CreateConsumeContext(new RunLeavePeriodGrant(2026, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        addedCredits.Should().BeEmpty();
    }
}
