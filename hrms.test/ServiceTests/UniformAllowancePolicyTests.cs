using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Services.Approvals;
using Hrms.Core.Services.UniformAllowance;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using Mapster;
using MapsterMapper;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>Builders shared by the uniform allowance policy tests.</summary>
internal static class UniformTestData
{
    public static UniformPolicySnapshot Policy(
        UniformCreditFrequency frequency = UniformCreditFrequency.Monthly,
        UniformAmountMode mode = UniformAmountMode.FixedPerPeriod,
        decimal amount = 500m,
        int creditDay = 1,
        int startMonth = 1,
        int? expectedDays = null,
        int waitingMonths = 0,
        int? minPresentDays = null,
        decimal? maxBalance = null,
        UniformBalanceExpiry expiry = UniformBalanceExpiry.CarryOver,
        int? expiryMonth = null,
        int? expiryAfterMonths = null,
        UniformRehireRule rehire = UniformRehireRule.Restart,
        int? bridgeDays = null,
        EmploymentStatus[]? statuses = null) => new(
        Guid.NewGuid(), new DateOnly(2020, 1, 1), true, frequency, creditDay, startMonth, mode, amount, expectedDays,
        statuses ?? [EmploymentStatus.Regular, EmploymentStatus.Probationary],
        waitingMonths, minPresentDays, maxBalance, expiry, expiryMonth, expiryAfterMonths,
        UniformSeparationRule.KeepForRelease, rehire, bridgeDays);

    public static UniformCreditCandidate Candidate(
        UniformPolicySnapshot policy, int presentDays = 22, EmploymentStatus status = EmploymentStatus.Regular,
        bool separated = false, DateOnly? serviceStart = null, DateOnly? creditDate = null) =>
        new(policy, status, separated, serviceStart ?? new DateOnly(2020, 1, 1), creditDate ?? new DateOnly(2026, 6, 1), presentDays);

    public static UpsertUniformAllowancePolicy Upsert(DateOnly effectiveFrom, decimal amount = 500m) => new()
    {
        EffectiveFrom = effectiveFrom,
        Amount = amount,
        EligibleStatuses = [EmploymentStatus.Regular],
        ChangeReason = "New client agreement",
    };
}

/// <summary>Which rules apply on a day: the latest version on or before it, else the legacy columns.</summary>
public class UniformAllowancePolicyResolverTests
{
    private static readonly Client Acme = new() { Id = Guid.NewGuid(), Name = "Acme", UniformAllowance = 300m };

    private static UniformAllowancePolicy Version(DateOnly from, decimal amount) =>
        new() { Id = Guid.NewGuid(), ClientId = Acme.Id, EffectiveFrom = from, Amount = amount };

    [Fact]
    public void PicksTheLatestVersionStartedByThatDay_AndTheLegacyRateBeforeTheFirst()
    {
        var versions = new[] { Version(new DateOnly(2026, 7, 1), 500m), Version(new DateOnly(2026, 10, 1), 800m) };

        UniformAllowancePolicyResolver.Resolve(versions, Acme, new DateOnly(2026, 6, 30))!.Should()
            .Match<UniformPolicySnapshot>(p => p.IsLegacy && p.Amount == 300m);
        UniformAllowancePolicyResolver.Resolve(versions, Acme, new DateOnly(2026, 9, 30))!.Amount.Should().Be(500m);
        UniformAllowancePolicyResolver.Resolve(versions, Acme, new DateOnly(2026, 10, 1))!.Amount.Should().Be(800m);
    }

    [Fact]
    public void IgnoresOtherClientsVersions_AndIsNullWithoutAnyRate()
    {
        var other = new UniformAllowancePolicy { ClientId = Guid.NewGuid(), EffectiveFrom = new DateOnly(2020, 1, 1), Amount = 900m };
        var noRate = new Client { Id = Guid.NewGuid() };

        UniformAllowancePolicyResolver.Resolve([other], noRate, new DateOnly(2026, 1, 1)).Should().BeNull();
    }
}

/// <summary>When credits post and which attendance window each one judges.</summary>
public class UniformCreditScheduleTests
{
    private static readonly DateOnly Hired = new(2020, 1, 1);

    [Fact]
    public void Quarterly_PostsOnTheCreditDayOfEachCycleMonth_CoveringTheQuarterBefore()
    {
        var policy = UniformTestData.Policy(UniformCreditFrequency.Quarterly, creditDay: 5, startMonth: 1);
        var schedule = UniformCreditSchedules.For(policy.Frequency);

        new[] { 1, 4, 7, 10 }.Should().OnlyContain(m => schedule.IsDue(new DateOnly(2026, m, 5), policy, Hired));
        schedule.IsDue(new DateOnly(2026, 2, 5), policy, Hired).Should().BeFalse();
        schedule.IsDue(new DateOnly(2026, 4, 1), policy, Hired).Should().BeFalse("credits post on the 5th");
        schedule.PeriodFor(new DateOnly(2026, 4, 5), policy).Should().Be((new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void SemiAnnualAndAnnual_AlignToTheStartMonth()
    {
        var semi = UniformTestData.Policy(UniformCreditFrequency.SemiAnnual, startMonth: 7);
        var annual = UniformTestData.Policy(UniformCreditFrequency.Annual, startMonth: 3);

        UniformCreditSchedules.For(semi.Frequency).IsDue(new DateOnly(2026, 1, 1), semi, Hired).Should().BeTrue();
        UniformCreditSchedules.For(semi.Frequency).IsDue(new DateOnly(2026, 4, 1), semi, Hired).Should().BeFalse();
        UniformCreditSchedules.For(annual.Frequency).IsDue(new DateOnly(2026, 3, 1), annual, Hired).Should().BeTrue();
        UniformCreditSchedules.For(annual.Frequency).PeriodFor(new DateOnly(2026, 3, 1), annual)
            .Should().Be((new DateOnly(2025, 3, 1), new DateOnly(2026, 2, 28)));
    }

    [Fact]
    public void HireAnniversary_PostsYearly_ALeapDayStartOnFeb28InOtherYears()
    {
        var policy = UniformTestData.Policy(UniformCreditFrequency.HireAnniversary);
        var schedule = UniformCreditSchedules.For(policy.Frequency);
        var leapStart = new DateOnly(2024, 2, 29);

        schedule.IsDue(leapStart, policy, leapStart).Should().BeFalse("not on the first day itself");
        schedule.IsDue(new DateOnly(2025, 2, 28), policy, leapStart).Should().BeTrue();
        schedule.IsDue(new DateOnly(2028, 2, 29), policy, leapStart).Should().BeTrue();
        schedule.IsDue(new DateOnly(2028, 2, 28), policy, leapStart).Should().BeFalse();
        schedule.PeriodFor(new DateOnly(2026, 5, 10), policy).Should().Be((new DateOnly(2025, 5, 10), new DateOnly(2026, 5, 9)));
    }

    [Fact]
    public void Upcoming_FollowsAScheduledChange_FromTheDayItStarts()
    {
        var monthly = UniformTestData.Policy(UniformCreditFrequency.Monthly, creditDay: 1);
        var quarterly = UniformTestData.Policy(UniformCreditFrequency.Quarterly, creditDay: 15, startMonth: 1);
        UniformPolicySnapshot PolicyOn(DateOnly day) => day < new DateOnly(2026, 9, 1) ? monthly : quarterly;

        UniformCreditSchedules.Upcoming(PolicyOn, new DateOnly(2026, 7, 2), Hired, 3).Select(x => x.Date).Should().Equal(
            new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 15), new DateOnly(2027, 1, 15));
    }
}

/// <summary>How much, who qualifies, and the cap -- the first failing check names the reason.</summary>
public class UniformCreditDeciderTests
{
    [Fact]
    public void Prorated_PaysTheShareOfExpectedDays_NeverMoreThanTheFullAmount()
    {
        var policy = UniformTestData.Policy(mode: UniformAmountMode.ProratedByPresentDays, amount: 600m, expectedDays: 26);

        UniformCreditDecider.Decide(UniformTestData.Candidate(policy, presentDays: 13), 0).Amount.Should().Be(300m);
        UniformCreditDecider.Decide(UniformTestData.Candidate(policy, presentDays: 30), 0).Amount.Should().Be(600m);
    }

    [Fact]
    public void PerPresentDay_PaysEachDay_AndSkipsAPeriodWithNone()
    {
        var policy = UniformTestData.Policy(mode: UniformAmountMode.PerPresentDay, amount: 25m);

        UniformCreditDecider.Decide(UniformTestData.Candidate(policy, presentDays: 18), 0).Amount.Should().Be(450m);
        UniformCreditDecider.Decide(UniformTestData.Candidate(policy, presentDays: 0), 0)
            .Reason!.Reason.Should().Be(UniformSkipReason.NoPresentDays);
    }

    [Fact]
    public void Threshold_SaysHowManyDaysWereShort()
    {
        var decision = UniformCreditDecider.Decide(
            UniformTestData.Candidate(UniformTestData.Policy(minPresentDays: 15), presentDays: 12), 0);

        decision.Outcome.Should().Be(UniformCreditOutcome.Skipped);
        decision.Reason!.Text.Should().Be("12 of 15 required present days");
    }

    [Theory]
    [InlineData(EmploymentStatus.OnLeave, UniformSkipReason.OnLeave)]
    [InlineData(EmploymentStatus.Suspended, UniformSkipReason.Suspended)]
    [InlineData(EmploymentStatus.Contract, UniformSkipReason.StatusNotEligible)]
    public void IneligibleStatuses_GiveTheirOwnReason(EmploymentStatus status, UniformSkipReason reason)
    {
        UniformCreditDecider.Decide(UniformTestData.Candidate(UniformTestData.Policy(), status: status), 0)
            .Reason!.Reason.Should().Be(reason);
    }

    [Fact]
    public void Separation_IsReportedBeforeAnythingElse()
    {
        var candidate = UniformTestData.Candidate(
            UniformTestData.Policy(waitingMonths: 6, minPresentDays: 20), presentDays: 0, separated: true,
            serviceStart: new DateOnly(2026, 5, 1));

        UniformCreditDecider.Decide(candidate, 0).Reason!.Reason.Should().Be(UniformSkipReason.Separated);
    }

    [Fact]
    public void WaitingPeriod_CountsWholeMonthsFromTheServiceStart()
    {
        var policy = UniformTestData.Policy(waitingMonths: 3);

        UniformCreditDecider.Decide(UniformTestData.Candidate(policy, serviceStart: new DateOnly(2026, 3, 1)), 0)
            .Outcome.Should().Be(UniformCreditOutcome.Credited);
        UniformCreditDecider.Decide(UniformTestData.Candidate(policy, serviceStart: new DateOnly(2026, 3, 2)), 0)
            .Reason!.Text.Should().Be("Waiting period: 2 of 3 month(s) of service");
    }

    [Fact]
    public void Cap_CreditsOnlyWhatFits_AndSkipsAFullBalance()
    {
        var policy = UniformTestData.Policy(amount: 500m, maxBalance: 1_200m);

        var capped = UniformCreditDecider.Decide(UniformTestData.Candidate(policy), 1_000m);
        capped.Outcome.Should().Be(UniformCreditOutcome.Capped);
        capped.Amount.Should().Be(200m);

        UniformCreditDecider.Decide(UniformTestData.Candidate(policy), 1_200m)
            .Reason!.Reason.Should().Be(UniformSkipReason.BalanceAtCap);
        UniformCreditDecider.Decide(UniformTestData.Candidate(policy), 500m)
            .Should().Match<UniformCreditDecision>(d => d.Outcome == UniformCreditOutcome.Credited && d.Amount == 500m);
    }
}

/// <summary>Separation, rehire and expiry rules.</summary>
public class UniformSeparationAndExpiryTests
{
    [Fact]
    public void Separated_ByStatus_OrByAResignationDateThatHasArrived()
    {
        var day = new DateOnly(2026, 6, 1);

        UniformSeparation.IsSeparated(EmploymentStatus.Resigned, null, day).Should().BeTrue();
        UniformSeparation.IsSeparated(EmploymentStatus.Regular, new DateTime(2026, 5, 31), day).Should().BeTrue();
        UniformSeparation.IsSeparated(EmploymentStatus.Regular, new DateTime(2026, 6, 30), day).Should().BeFalse("not yet");
        UniformSeparation.SeparatedOn(new DateTime(2026, 5, 31), day).Should().Be(new DateOnly(2026, 5, 31));
        UniformSeparation.SeparatedOn(null, day).Should().Be(day);
    }

    [Theory]
    [InlineData(UniformRehireRule.Restart, null, "2026-07-15", "2026-07-15")]
    [InlineData(UniformRehireRule.BridgeWithinDays, 60, "2026-07-15", "2021-03-01")] // back within 45 days
    [InlineData(UniformRehireRule.BridgeWithinDays, 30, "2026-07-15", "2026-07-15")] // too long away
    public void Rehire_RestartsTheServiceClock_UnlessBridgedInsideTheWindow(
        UniformRehireRule rule, int? bridgeDays, string newHireDate, string expectedStart)
    {
        var policy = UniformTestData.Policy(rehire: rule, bridgeDays: bridgeDays);

        UniformSeparation.ServiceStartAfterRehire(
                policy, new DateOnly(2021, 3, 1), DateOnly.Parse(newHireDate), new DateOnly(2026, 5, 31), new DateOnly(2026, 8, 1))
            .Should().Be(DateOnly.Parse(expectedStart));
    }

    [Fact]
    public void Rehire_WithAnUnchangedHireDate_CountsFromTheDayTheReturnWasNoticed()
    {
        UniformSeparation.ServiceStartAfterRehire(
                UniformTestData.Policy(), new DateOnly(2021, 3, 1), new DateOnly(2021, 3, 1), new DateOnly(2026, 5, 31), new DateOnly(2026, 8, 1))
            .Should().Be(new DateOnly(2026, 8, 1));
    }

    [Fact]
    public void YearlyReset_ClearsTheBalance_OnlyOnTheFirstOfItsMonth()
    {
        var policy = UniformTestData.Policy(expiry: UniformBalanceExpiry.ResetAnnually, expiryMonth: 1);

        UniformExpiry.Due(policy, new DateOnly(2027, 1, 1), 1_500m, 0).Should().Be(1_500m);
        UniformExpiry.Due(policy, new DateOnly(2027, 1, 2), 1_500m, 0).Should().Be(0);
        UniformExpiry.Due(policy, new DateOnly(2027, 2, 1), 1_500m, 0).Should().Be(0);
    }

    [Fact]
    public void RollingExpiry_LapsesOnlyMoneyOlderThanTheWindow()
    {
        var policy = UniformTestData.Policy(expiry: UniformBalanceExpiry.ExpireAfterMonths, expiryAfterMonths: 6);
        var day = new DateOnly(2026, 9, 1);

        UniformExpiry.FreshSince(policy, day).Should().Be(new DateOnly(2026, 3, 1));
        UniformExpiry.Due(policy, day, balance: 2_000m, freshCredits: 1_500m).Should().Be(500m);
        UniformExpiry.Due(policy, day, balance: 1_000m, freshCredits: 1_500m).Should().Be(0, "everything left is recent");
    }

    [Fact]
    public void Particulars_NameThePeriodCovered()
    {
        UniformAllowanceAccrualWorker.Particulars(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), UniformAmountMode.FixedPerPeriod, null)
            .Should().Be("Uniform allowance — Sep 2026");
        UniformAllowanceAccrualWorker.Particulars(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), UniformAmountMode.PerPresentDay, 61)
            .Should().Be("Uniform allowance — Jul–Sep 2026 (61 present days)");
        UniformAllowanceAccrualWorker.Particulars(new DateOnly(2025, 10, 1), new DateOnly(2026, 9, 30), UniformAmountMode.FixedPerPeriod, null)
            .Should().Be("Uniform allowance — Oct 2025 – Sep 2026");
    }
}

/// <summary>Saving versions: validation, no backdating, and settled versions stay as they were.</summary>
public class UniformAllowancePolicyServiceTests
{
    private static readonly DateOnly Today = Hrms.Core.Services.PhTime.Today(DateTime.UtcNow);

    // A client with one employee, for credit-log rows to point at.
    private static Guid SeedClient(Hrms.Infrastructure.HrmsContext seed, Guid clientId, decimal? legacyRate = null)
    {
        var group = new PayrollGroup { Id = Guid.NewGuid(), Name = "Main" };
        var employee = new Employee
        {
            Id = Guid.NewGuid(), FirstName = "Ana", LastName = "Test", JobLevel = "", BioId = Random.Shared.Next(1, int.MaxValue),
            PayrollGroupId = group.Id, ClientId = clientId, HireDate = new DateOnly(2020, 1, 1),
        };
        seed.PayrollGroups.Add(group);
        seed.Clients.Add(new Client { Id = clientId, Name = "Acme", Code = "AC", UniformAllowance = legacyRate });
        seed.Employees.Add(employee);
        return employee.Id;
    }

    [Fact]
    public void Validate_NamesEveryMissingPiece()
    {
        var payload = new UpsertUniformAllowancePolicy
        {
            Amount = 0,
            CreditDay = 31,
            AmountMode = UniformAmountMode.ProratedByPresentDays,
            Expiry = UniformBalanceExpiry.ResetAnnually,
            RehireRule = UniformRehireRule.BridgeWithinDays,
            EligibleStatuses = [EmploymentStatus.Resigned],
        };

        UniformAllowancePolicyService.Validate(payload).Should().HaveCount(7);
        UniformAllowancePolicyService.Validate(UniformTestData.Upsert(Today)).Should().BeEmpty();
        UniformAllowancePolicyService.Validate(new UpsertUniformAllowancePolicy { IsEnabled = false, ChangeReason = "Ended" })
            .Should().BeEmpty("a disabled version needs only the reason");
    }

    [Fact]
    public async Task Create_RejectsAPastStart_AndAStartOnOrBeforeTheLastDecidedCredit()
    {
        using var db = new SqliteHrmsContext();
        var clientId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var employeeId = SeedClient(seed, clientId);
            seed.UniformAllowanceCreditLogs.Add(new UniformAllowanceCreditLog
            {
                EmployeeId = employeeId, ClientId = clientId, CreditDate = Today.AddDays(3),
                Outcome = UniformCreditOutcome.Skipped,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new UniformAllowancePolicyService(new UnitOfWorkService(context));

        await service.Invoking(s => s.CreateAsync(clientId, UniformTestData.Upsert(Today.AddDays(-1)), null, "HR", default))
            .Should().ThrowAsync<Hrms.Core.ValidationException>().WithMessage("*today at the earliest*");
        await service.Invoking(s => s.CreateAsync(clientId, UniformTestData.Upsert(Today.AddDays(3)), null, "HR", default))
            .Should().ThrowAsync<Hrms.Core.ValidationException>().WithMessage("*already decided*");

        var saved = await service.CreateAsync(clientId, UniformTestData.Upsert(Today.AddDays(4)), null, "HR", default);
        saved.CreatedByName.Should().Be("HR");
        saved.ChangeReason.Should().Be("New client agreement");
    }

    [Fact]
    public async Task List_LocksVersionsTheRunHasUsed_AndShowsTheLegacySettings()
    {
        using var db = new SqliteHrmsContext();
        var clientId = Guid.NewGuid();
        var used = new UniformAllowancePolicy { Id = Guid.NewGuid(), ClientId = clientId, EffectiveFrom = Today.AddDays(-30), Amount = 400m };
        var future = new UniformAllowancePolicy { Id = Guid.NewGuid(), ClientId = clientId, EffectiveFrom = Today.AddDays(10), Amount = 600m };
        await using (var seed = db.NewContext())
        {
            var employeeId = SeedClient(seed, clientId, legacyRate: 300m);
            seed.UniformAllowancePolicies.AddRange(used, future);
            seed.UniformAllowanceCreditLogs.Add(new UniformAllowanceCreditLog
            {
                EmployeeId = employeeId, ClientId = clientId, CreditDate = Today.AddDays(-1), PolicyId = used.Id,
                Outcome = UniformCreditOutcome.Credited,
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new UniformAllowancePolicyService(new UnitOfWorkService(context));
        var list = await service.ListAsync(clientId, default);

        list.CurrentPolicyId.Should().Be(used.Id);
        list.Versions.Single(v => v.Id == used.Id).IsLocked.Should().BeTrue();
        list.Versions.Single(v => v.Id == future.Id).IsLocked.Should().BeFalse();
        list.Legacy!.Amount.Should().Be(300m);
        await service.Invoking(s => s.DeleteAsync(clientId, used.Id, default)).Should().ThrowAsync<Hrms.Core.ValidationException>();
        await service.DeleteAsync(clientId, future.Id, default);
        (await service.VersionsAsync(clientId, default)).Should().ContainSingle();
    }
}

/// <summary>The daily run against SQLite: credits, skips with reasons, caps, separation, and re-runs.</summary>
public class UniformAllowanceAccrualWorkerTests : IDisposable
{
    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public void Dispose() => _db.Dispose();

    private Employee Employee(string name, DateOnly hired, EmploymentStatus status = EmploymentStatus.Regular, DateTime? resigned = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = name, LastName = "Test", JobLevel = "", BioId = Random.Shared.Next(1, int.MaxValue),
        PayrollGroupId = _groupId, ClientId = _clientId, HireDate = hired, EmploymentStatus = status, DateResigned = resigned,
    };

    private static IEnumerable<DailyRecord> PresentDays(Employee e, int year, int month, int count) =>
        Enumerable.Range(1, count).Select(day => new DailyRecord
        {
            Id = Guid.NewGuid(), EmployeeId = e.Id, FullName = e.FirstName, WorkDate = new DateOnly(year, month, day),
            BatchCode = "B1", Posted = true, RegularNetHours = 8,
        });

    private async Task SeedAsync(Action<Hrms.Infrastructure.HrmsContext> add)
    {
        await using var seed = _db.NewContext();
        if (!seed.Clients.Any(c => c.Id == _clientId))
        {
            seed.PayrollGroups.Add(new PayrollGroup { Id = _groupId, Name = "Main" });
            seed.Clients.Add(new Client { Id = _clientId, Name = "Acme", Code = "AC" });
        }
        add(seed);
        await seed.SaveChangesAsync();
    }

    private UniformAllowancePolicy Version(DateOnly from, Action<UniformAllowancePolicy>? configure = null)
    {
        var policy = new UniformAllowancePolicy
        {
            Id = Guid.NewGuid(), ClientId = _clientId, EffectiveFrom = from, Amount = 500m,
            EligibleStatuses = [EmploymentStatus.Regular], ChangeReason = "test",
        };
        configure?.Invoke(policy);
        return policy;
    }

    private async Task RunAsync(DateOnly date)
    {
        await using var context = _db.NewContext();
        var uow = new UnitOfWorkService(context);
        var dailyRecords = new DailyRecordService(uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
            NullLogger<DailyRecordService>.Instance,
            new LeaveDtrReconciliationService(uow, NullLogger<LeaveDtrReconciliationService>.Instance),
            new PayrollBatchService(uow), new DTRBatchService(uow),
            new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()));
        await new UniformAllowanceAccrualWorker(uow, new UniformAllowanceFundService(uow), dailyRecords,
                NullLogger<UniformAllowanceAccrualWorker>.Instance)
            .RunAsync(new RunUniformAllowanceAccrual(date), default);
    }

    private async Task<List<UniformAllowanceCreditLog>> LogsAsync()
    {
        await using var context = _db.NewContext();
        return await context.UniformAllowanceCreditLogs.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task CreditsWhoQualify_LogsEverySkipWithItsReason_AndARerunChangesNothing()
    {
        var ana = Employee("Ana", new DateOnly(2020, 1, 1));
        var ben = Employee("Ben", new DateOnly(2020, 1, 1));
        var cy = Employee("Cy", new DateOnly(2020, 1, 1), EmploymentStatus.OnLeave);
        var policy = Version(new DateOnly(2026, 1, 1), p => p.MinPresentDays = 15);
        await SeedAsync(seed =>
        {
            seed.Employees.AddRange(ana, ben, cy);
            seed.UniformAllowancePolicies.Add(policy);
            seed.DailyTimeRecords.AddRange(PresentDays(ana, 2026, 5, 16));
            seed.DailyTimeRecords.AddRange(PresentDays(ben, 2026, 5, 12));
        });

        await RunAsync(new DateOnly(2026, 6, 1));
        await RunAsync(new DateOnly(2026, 6, 1));

        var logs = await LogsAsync();
        logs.Should().HaveCount(3);
        logs.Single(l => l.EmployeeId == ana.Id).Should().Match<UniformAllowanceCreditLog>(l =>
            l.Outcome == UniformCreditOutcome.Credited && l.Amount == 500m && l.PolicyId == policy.Id &&
            l.PresentDays == 16 && l.PeriodStart == new DateOnly(2026, 5, 1) && l.LedgerId != null);
        logs.Single(l => l.EmployeeId == ben.Id).ReasonText.Should().Be("12 of 15 required present days");
        logs.Single(l => l.EmployeeId == cy.Id).ReasonCode.Should().Be(nameof(UniformSkipReason.OnLeave));

        await using var context = _db.NewContext();
        var ledger = await context.UniformAllowanceLedgers.AsNoTracking().ToListAsync();
        ledger.Should().ContainSingle().Which.Should().Match<UniformAllowanceLedger>(l =>
            l.EmployeeId == ana.Id && l.Particulars == "Uniform allowance — May 2026" && l.PolicyId == policy.Id);
        (await context.UniformAllowanceFunds.SingleAsync(f => f.EmployeeId == ana.Id)).Balance.Should().Be(500m);
    }

    [Fact]
    public async Task OnlyTheCreditDayCredits_UnderTheVersionInForceThatDay()
    {
        var ana = Employee("Ana", new DateOnly(2020, 1, 1));
        var first = Version(new DateOnly(2026, 1, 1), p => p.CreditDay = 15);
        var raise = Version(new DateOnly(2026, 7, 1), p => { p.CreditDay = 15; p.Amount = 800m; });
        await SeedAsync(seed =>
        {
            seed.Employees.Add(ana);
            seed.UniformAllowancePolicies.AddRange(first, raise);
        });

        await RunAsync(new DateOnly(2026, 6, 14));
        await RunAsync(new DateOnly(2026, 6, 15));
        await RunAsync(new DateOnly(2026, 7, 15));

        var logs = await LogsAsync();
        logs.Select(l => (l.CreditDate, l.Amount, l.PolicyId)).Should().BeEquivalentTo(
        [
            (new DateOnly(2026, 6, 15), 500m, (Guid?)first.Id),
            (new DateOnly(2026, 7, 15), 800m, (Guid?)raise.Id),
        ]);
    }

    [Fact]
    public async Task Cap_CreditsOnlyUpToTheLimit()
    {
        var ana = Employee("Ana", new DateOnly(2020, 1, 1));
        await SeedAsync(seed =>
        {
            seed.Employees.Add(ana);
            seed.UniformAllowancePolicies.Add(Version(new DateOnly(2026, 1, 1), p => p.MaxBalance = 1_200m));
            seed.UniformAllowanceFunds.Add(new UniformAllowanceFund { EmployeeId = ana.Id, Balance = 1_000m });
        });

        await RunAsync(new DateOnly(2026, 6, 1));

        var log = (await LogsAsync()).Single();
        log.Outcome.Should().Be(UniformCreditOutcome.Capped);
        log.Amount.Should().Be(200m);
        await using var context = _db.NewContext();
        (await context.UniformAllowanceFunds.SingleAsync()).Balance.Should().Be(1_200m);
    }

    [Fact]
    public async Task Separation_ForfeitsUnderAForfeitPolicy_AndABridgedRehireKeepsTheirService()
    {
        var ana = Employee("Ana", new DateOnly(2021, 3, 1), EmploymentStatus.Resigned, new DateTime(2026, 5, 20));
        await SeedAsync(seed =>
        {
            seed.Employees.Add(ana);
            seed.UniformAllowancePolicies.Add(Version(new DateOnly(2026, 1, 1), p =>
            {
                p.OnSeparation = UniformSeparationRule.Forfeit;
                p.RehireRule = UniformRehireRule.BridgeWithinDays;
                p.RehireBridgeDays = 60;
                p.WaitingMonths = 6;
            }));
            seed.UniformAllowanceFunds.Add(new UniformAllowanceFund { EmployeeId = ana.Id, Balance = 900m });
        });

        await RunAsync(new DateOnly(2026, 6, 1));

        await using (var context = _db.NewContext())
        {
            var fund = await context.UniformAllowanceFunds.SingleAsync();
            fund.Should().Match<UniformAllowanceFund>(f =>
                f.Balance == 0 && f.SeparatedOn == new DateOnly(2026, 5, 20) && f.ServiceStartDate == new DateOnly(2021, 3, 1));
            (await context.UniformAllowanceLedgers.SingleAsync()).EntryType.Should().Be(UniformAllowanceEntryType.Forfeiture);
            (await context.UniformAllowanceCreditLogs.SingleAsync()).ReasonCode.Should().Be(nameof(UniformSkipReason.Separated));

            // Rehired 26 days after leaving.
            var employee = await context.Employees.SingleAsync();
            employee.EmploymentStatus = EmploymentStatus.Regular;
            employee.DateResigned = null;
            employee.HireDate = new DateOnly(2026, 6, 15);
            await context.SaveChangesAsync();
        }

        await RunAsync(new DateOnly(2026, 7, 1));

        await using var verify = _db.NewContext();
        var rehired = await verify.UniformAllowanceFunds.SingleAsync();
        rehired.SeparatedOn.Should().BeNull();
        rehired.ServiceStartDate.Should().Be(new DateOnly(2021, 3, 1), "back within the 60-day bridge");
        (await verify.UniformAllowanceCreditLogs.SingleAsync(l => l.CreditDate == new DateOnly(2026, 7, 1)))
            .Outcome.Should().Be(UniformCreditOutcome.Credited, "the bridged service is past the waiting period");
    }

    [Fact]
    public async Task LegacyClient_KeepsItsMonthlyCredit_WithoutAnyPolicyVersion()
    {
        var ana = Employee("Ana", new DateOnly(2020, 1, 1));
        await SeedAsync(seed =>
        {
            seed.Employees.Add(ana);
            seed.Clients.Local.Single(c => c.Id == _clientId).UniformAllowance = 350m;
        });

        await RunAsync(new DateOnly(2026, 6, 1));

        (await LogsAsync()).Single().Should().Match<UniformAllowanceCreditLog>(l =>
            l.Outcome == UniformCreditOutcome.Credited && l.Amount == 350m && l.PolicyId == null);
    }

    [Fact]
    public async Task YearlyReset_ExpiresTheBalance_BeforeThatDaysCredit()
    {
        var ana = Employee("Ana", new DateOnly(2020, 1, 1));
        await SeedAsync(seed =>
        {
            seed.Employees.Add(ana);
            seed.UniformAllowancePolicies.Add(Version(new DateOnly(2026, 1, 1), p =>
            {
                p.Expiry = UniformBalanceExpiry.ResetAnnually;
                p.ExpiryMonth = 1;
            }));
            seed.UniformAllowanceFunds.Add(new UniformAllowanceFund { EmployeeId = ana.Id, Balance = 2_000m });
        });

        await RunAsync(new DateOnly(2027, 1, 1));

        await using var context = _db.NewContext();
        var entries = (await context.UniformAllowanceLedgers.AsNoTracking().ToListAsync()).OrderBy(x => x.Balance).ToList();
        entries.Select(e => (e.EntryType, e.Amount)).Should().BeEquivalentTo(
        [
            (UniformAllowanceEntryType.Expiry, -2_000m),
            (UniformAllowanceEntryType.Accrual, 500m),
        ]);
        (await context.UniformAllowanceFunds.SingleAsync()).Balance.Should().Be(500m);
    }
}
