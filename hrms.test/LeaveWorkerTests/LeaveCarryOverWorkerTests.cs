using hrms.test.LeaveWorkerTests.TestSupport;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// LeaveCarryOverWorker — runs at fiscal year-end, applying each leave type's CarryOverType
/// to the closing balance: Forfeit expires everything, Unlimited carries everything, Capped
/// splits the balance at leave.CarryOverMaxDays. A carried amount creates next year's
/// LeaveCredits record (skipped if one already exists — the pre-check idempotency guard);
/// an expired amount always debits the closing credit's own Balance regardless of whether the
/// carry-forward half also fires.
/// </summary>
public class LeaveCarryOverWorkerTests : LeaveWorkerTestBase
{
    private const int FromYear = 2026;
    private const int NextYear = 2027;
    private static readonly DateOnly CloseDate = FiscalYearHelper.LastDayOfFiscalYear(FromYear, 1);

    private static (LeaveCarryOverWorker Worker, IRepository Repo) BuildWorker()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        var worker = new LeaveCarryOverWorker(uow, CreateLogger<LeaveCarryOverWorker>());
        return (worker, repo);
    }

    private static void SeedNoAlreadyProcessedOrNextYear(IRepository repo)
    {
        SeedFind(repo, Array.Empty<LeaveLedger>());
    }

    [Fact]
    public async Task ForfeitType_ExpiresTheEntireBalance_NoCarryOver()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, carryOverType: CarryOverType.Forfeit);
        var credit = BuildCredits(leave, NewId(), FromYear, granted: 10, balance: 10);
        SeedFind(repo, credit);
        SeedNoAlreadyProcessedOrNextYear(repo);
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(0m);
        addedCredits.Should().BeEmpty();
        addedLedgers.Should().ContainSingle();
        addedLedgers[0].EntryType.Should().Be(LedgerEntryType.Expiry);
        addedLedgers[0].Less.Should().Be(10m);
        addedLedgers[0].EntryDate.Should().Be(CloseDate);
    }

    [Fact]
    public async Task UnlimitedType_CarriesTheEntireBalance_NoExpiry()
    {
        var (worker, repo) = BuildWorker();
        var employeeId = NewId();
        var leave = BuildLeave(AccrualBasis.None, carryOverType: CarryOverType.Unlimited);
        var credit = BuildCredits(leave, employeeId, FromYear, granted: 10, balance: 10);
        SeedFind(repo, credit);
        SeedNoAlreadyProcessedOrNextYear(repo);
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(10m); // untouched -- nothing expired
        addedCredits.Should().ContainSingle();
        addedCredits[0].PeriodYear.Should().Be(NextYear);
        addedCredits[0].EmployeeId.Should().Be(employeeId);
        addedCredits[0].Granted.Should().Be(10m);
        addedCredits[0].Balance.Should().Be(10m);

        addedLedgers.Should().ContainSingle();
        addedLedgers[0].EntryType.Should().Be(LedgerEntryType.CarryOver);
        addedLedgers[0].Add.Should().Be(10m);
        addedLedgers[0].LeaveCreditsId.Should().Be(addedCredits[0].Id);
    }

    [Fact]
    public async Task CappedType_SplitsBetweenCarryOverAndExpiry()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, carryOverType: CarryOverType.Capped, carryOverMaxDays: 6);
        var credit = BuildCredits(leave, NewId(), FromYear, granted: 10, balance: 10);
        SeedFind(repo, credit);
        SeedNoAlreadyProcessedOrNextYear(repo);
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(6m); // 10 - 4 expired
        addedCredits.Should().ContainSingle();
        addedCredits[0].Granted.Should().Be(6m);

        addedLedgers.Should().HaveCount(2);
        addedLedgers.Should().Contain(l => l.EntryType == LedgerEntryType.Expiry && l.Less == 4m);
        addedLedgers.Should().Contain(l => l.EntryType == LedgerEntryType.CarryOver && l.Add == 6m);
    }

    [Fact]
    public async Task PerEventLeaveType_IsNeverCarriedOverOrExpired()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.PerEvent, carryOverType: CarryOverType.Unlimited);
        var credit = BuildCredits(leave, NewId(), FromYear, granted: 10, balance: 10);
        SeedFind(repo, credit);
        SeedNoAlreadyProcessedOrNextYear(repo);
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(10m);
        addedCredits.Should().BeEmpty();
        addedLedgers.Should().BeEmpty();
    }

    [Fact]
    public async Task AlreadyProcessedCredit_IsSkipped_Idempotency()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.None, carryOverType: CarryOverType.Forfeit);
        var credit = BuildCredits(leave, NewId(), FromYear, granted: 10, balance: 10);
        SeedFind(repo, credit);
        SeedFind(repo, new LeaveLedger
        {
            Id = NewId(),
            EmployeeId = credit.EmployeeId,
            LeaveId = leave.Id,
            LeaveCreditsId = credit.Id,
            EntryType = LedgerEntryType.Expiry,
            EntryDate = CloseDate,
        });
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(10m); // untouched
        addedLedgers.Should().BeEmpty();
    }

    [Fact]
    public async Task CarryOverButNextYearCreditAlreadyExists_SkipsCarryButStillExpiresTheRest()
    {
        var (worker, repo) = BuildWorker();
        var employeeId = NewId();
        var leave = BuildLeave(AccrualBasis.None, carryOverType: CarryOverType.Capped, carryOverMaxDays: 6);
        var credit = BuildCredits(leave, employeeId, FromYear, granted: 10, balance: 10);
        SeedFind(repo, credit, BuildCredits(leave, employeeId, NextYear, granted: 6)); // next-year row pre-exists
        SeedFind(repo, Array.Empty<LeaveLedger>());
        var addedCredits = CaptureAdds<LeaveCredits>(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(6m); // expiry portion still applied
        addedCredits.Should().BeEmpty(); // no duplicate next-year credit created
        addedLedgers.Should().ContainSingle();
        addedLedgers[0].EntryType.Should().Be(LedgerEntryType.Expiry);
    }

    [Fact]
    public async Task CreditWithNoLeaveNavigationLoaded_IsSkippedDefensively()
    {
        var (worker, repo) = BuildWorker();
        var orphanCredit = new LeaveCredits
        {
            Id = NewId(),
            EmployeeId = NewId(),
            LeaveId = NewId(),
            Leave = null!,
            PeriodYear = FromYear,
            FromDate = new DateTime(FromYear, 1, 1),
            ToDate = new DateTime(FromYear, 12, 31),
            Granted = 10,
            Balance = 10,
        };
        SeedFind(repo, orphanCredit);
        SeedNoAlreadyProcessedOrNextYear(repo);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveCarryOver(FromYear, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        orphanCredit.Balance.Should().Be(10m);
        addedLedgers.Should().BeEmpty();
    }
}
