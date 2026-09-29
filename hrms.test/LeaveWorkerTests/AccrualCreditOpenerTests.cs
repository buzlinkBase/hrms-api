using hrms.test.LeaveWorkerTests.TestSupport;

namespace hrms.test.LeaveWorkerTests;

/// <summary>
/// AccrualCreditOpener -- the missing piece that made accrual-based leave types accrue for
/// nobody: LeaveAccrualWorker only tops up records that already exist, and nothing created them
/// for Monthly/PerPayPeriod/Annually types (the period grant only handles lump-sum types).
/// </summary>
public class AccrualCreditOpenerTests : LeaveWorkerTestBase
{
    private static readonly DateOnly MarchFirst = new(2026, 3, 1);

    private static (AccrualCreditOpener Opener, IRepository Repo) Build()
    {
        var repo = CreateRepo();
        var uow = CreateUow(repo);
        return (new AccrualCreditOpener(uow, BuildDailyRecordService(uow), CreateLogger<AccrualCreditOpener>()), repo);
    }

    [Fact]
    public async Task OpensAnEmptyRecordForTheFiscalYear_ForEachEligibleActiveEmployee()
    {
        var (opener, repo) = Build();
        var monthly = BuildLeave(AccrualBasis.Monthly, accrualRate: 1.25);
        var active = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        var resigned = BuildEmployee(EmploymentStatus.Resigned, hireDate: new DateOnly(2020, 1, 1));
        SeedFind(repo, monthly);
        SeedFind(repo, active, resigned);
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var added = CaptureAdds<LeaveCredits>(repo);

        var opened = await opener.OpenMissingAsync(MarchFirst, fiscalStartMonth: 1, CancellationToken.None);

        opened.Should().Be(1);
        var record = added.Should().ContainSingle().Subject;
        record.EmployeeId.Should().Be(active.Id);
        record.LeaveId.Should().Be(monthly.Id);
        record.PeriodYear.Should().Be(2026);
        record.FromDate.Should().Be(new DateTime(2026, 1, 1));
        record.Granted.Should().Be(0m);
        record.Balance.Should().Be(0m);
    }

    [Fact]
    public async Task UsesTheCompanysFiscalYear()
    {
        var (opener, repo) = Build();
        SeedFind(repo, BuildLeave(AccrualBasis.Monthly, accrualRate: 1));
        SeedFind(repo, BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1)));
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var added = CaptureAdds<LeaveCredits>(repo);

        // Fiscal year starting April: March 2026 belongs to FY2025 (Apr 2025 - Mar 2026).
        await opener.OpenMissingAsync(MarchFirst, fiscalStartMonth: 4, CancellationToken.None);

        var record = added.Single();
        record.PeriodYear.Should().Be(2025);
        record.FromDate.Should().Be(new DateTime(2025, 4, 1));
        record.ToDate.Date.Should().Be(new DateTime(2026, 3, 31));
    }

    [Fact]
    public async Task SkipsEmployeesWhoAlreadyHaveARecord_OrAreNotYetEligible()
    {
        var (opener, repo) = Build();
        var leave = BuildLeave(AccrualBasis.Monthly, accrualRate: 1, minServiceMonths: 6);
        var hasRecord = BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1));
        var tooNew = BuildEmployee(EmploymentStatus.Probationary, hireDate: new DateOnly(2026, 1, 15));
        SeedFind(repo, leave);
        SeedFind(repo, hasRecord, tooNew);
        SeedFind(repo, BuildCredits(leave, hasRecord.Id, 2026, granted: 2));
        var added = CaptureAdds<LeaveCredits>(repo);

        var opened = await opener.OpenMissingAsync(MarchFirst, fiscalStartMonth: 1, CancellationToken.None);

        opened.Should().Be(0);
        added.Should().BeEmpty();
    }

    [Fact]
    public async Task AnnuallyAccruedTypes_AreOnlyOpenedOnTheFiscalYearsFirstDay()
    {
        var (opener, repo) = Build();
        SeedFind(repo, BuildLeave(AccrualBasis.Annually, accrualRate: 5));
        SeedFind(repo, BuildEmployee(EmploymentStatus.Regular, hireDate: new DateOnly(2020, 1, 1)));
        SeedFind(repo, Array.Empty<LeaveCredits>());
        var added = CaptureAdds<LeaveCredits>(repo);

        (await opener.OpenMissingAsync(MarchFirst, fiscalStartMonth: 1, CancellationToken.None)).Should().Be(0);
        (await opener.OpenMissingAsync(MarchFirst, fiscalStartMonth: 3, CancellationToken.None)).Should().Be(1);
        added.Should().ContainSingle();
    }
}
