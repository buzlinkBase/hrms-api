using hrms.test.LeaveWorkerTests.TestSupport;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// LeaveAccrualWorker — runs on the 1st of each month, accruing Monthly/Annually/PerPayPeriod
/// leave types onto every active LeaveCredits record covering the process date. Annually only
/// fires on the fiscal year's first day (FiscalYearHelper.IsFiscalYearStart, already covered in
/// FiscalYearHelperTests). Idempotency is enforced by checking for an existing Accrual
/// LeaveLedger entry for the same credit + year + month before crediting again.
/// </summary>
public class LeaveAccrualWorkerTests : LeaveWorkerTestBase
{
    private static readonly DateOnly ProcessDate = new(2026, 3, 1);

    private static (LeaveAccrualWorker Worker, IRepository Repo) BuildWorker()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        var worker = new LeaveAccrualWorker(uow, CreateLogger<LeaveAccrualWorker>());
        return (worker, repo);
    }

    [Fact]
    public async Task MonthlyAccrual_CreditWithinRange_IncreasesGrantedAndBalanceAndLogsLedger()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 1.5);
        var credit = BuildCredits(leave, NewId(), 2026, granted: 10, balance: 10);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>()); // nothing accrued yet this month
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Granted.Should().Be(11.5m);
        credit.Balance.Should().Be(11.5m);
        addedLedgers.Should().ContainSingle();
        addedLedgers[0].EntryType.Should().Be(LedgerEntryType.Accrual);
        addedLedgers[0].Add.Should().Be(1.5m);
        addedLedgers[0].LeaveCreditsId.Should().Be(credit.Id);
    }

    [Fact]
    public async Task AnnualAccrual_OnFiscalYearStart_Accrues()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Annually, accrualRate: 5);
        var credit = BuildCredits(leave, NewId(), 2026, granted: 0, balance: 0);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>());
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        // startMonth=3 makes Mar 1 the fiscal year start -> Annually is eligible.
        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 3));
        await worker.Consume(context);

        credit.Balance.Should().Be(5m);
        addedLedgers.Should().ContainSingle();
    }

    [Fact]
    public async Task AnnualAccrual_NotOnFiscalYearStart_IsSkipped()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Annually, accrualRate: 5);
        var credit = BuildCredits(leave, NewId(), 2026, granted: 0, balance: 0);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>());
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        // startMonth=4 means Mar 1 is NOT the fiscal year start.
        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 4));
        await worker.Consume(context);

        credit.Balance.Should().Be(0m);
        addedLedgers.Should().BeEmpty();
    }

    [Fact]
    public async Task ZeroOrNegativeAccrualRate_IsSkippedEntirely()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 0);
        var credit = BuildCredits(leave, NewId(), 2026, granted: 10, balance: 10);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>());
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(10m);
        addedLedgers.Should().BeEmpty();
    }

    [Fact]
    public async Task MaxAccrualBalance_CapsTheEarnedAmount()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 3);
        leave.MaxAccrualBalance = 11; // only 1 more unit of headroom
        var credit = BuildCredits(leave, NewId(), 2026, granted: 10, balance: 10);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>());
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(11m);
        addedLedgers.Should().ContainSingle();
        addedLedgers[0].Add.Should().Be(1m); // capped, not the full 3
    }

    [Fact]
    public async Task AlreadyAtOrAboveMaxAccrualBalance_EarnsNothing_NoLedgerEntry()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 3);
        leave.MaxAccrualBalance = 10;
        var credit = BuildCredits(leave, NewId(), 2026, granted: 10, balance: 10);
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>());
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(10m);
        addedLedgers.Should().BeEmpty();
    }

    [Fact]
    public async Task CreditAlreadyAccruedThisMonth_IsSkipped_Idempotency()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 2);
        var credit = BuildCredits(leave, NewId(), 2026, granted: 10, balance: 10);
        SeedFind(repo, leave);
        SeedFind(repo, new LeaveLedger
        {
            Id = NewId(),
            EmployeeId = credit.EmployeeId,
            LeaveId = leave.Id,
            LeaveCreditsId = credit.Id,
            EntryType = LedgerEntryType.Accrual,
            EntryDate = ProcessDate,
        });
        SeedFind(repo, credit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        credit.Balance.Should().Be(10m); // untouched
        addedLedgers.Should().BeEmpty();
    }

    [Fact]
    public async Task CreditOutsideTheDateRange_IsNeverConsidered()
    {
        var (worker, repo) = BuildWorker();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 2);
        var expiredCredit = BuildCredits(leave, NewId(), 2025, granted: 10, balance: 10);
        expiredCredit.ToDate = new DateTime(2025, 12, 31); // ended before the 2026-03-01 process date
        SeedFind(repo, leave);
        SeedFind(repo, Array.Empty<LeaveLedger>());
        SeedFind(repo, expiredCredit);
        var addedLedgers = CaptureAdds<LeaveLedger>(repo);

        var context = CreateConsumeContext(new RunLeaveAccrual(ProcessDate, FiscalYearStartMonth: 1));
        await worker.Consume(context);

        expiredCredit.Balance.Should().Be(10m);
        addedLedgers.Should().BeEmpty();
    }
}
