using Hrms.Core.Calculators.Payloads;
using Hrms.Core.Policies.DeductionPolicies;
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

namespace hrms.test.PayrollRunTests;

/// <summary>
/// A reliever paid by several per-client runs in one cutoff is deducted in exactly one of them:
/// the deduction period maps every client's run to the same home cutoff, the carrier rule picks
/// the run, and PayrollDeductionClaim's unique index guarantees it.
/// </summary>
public class DeductionCarrierTests
{
    private static readonly Guid ClientA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid ClientB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    // ---------- DeductionPeriodResolver ----------

    [Theory]
    [InlineData(2026, 1, 10, 1, 15)]
    [InlineData(2026, 1, 15, 1, 15)]
    [InlineData(2026, 1, 16, 16, 31)]
    [InlineData(2026, 2, 20, 16, 28)]
    public void Period_SemiMonthly_IsTheCutoffTheDateFallsIn(int y, int m, int d, int startDay, int endDay)
    {
        var period = DeductionPeriodResolver.Resolve(
            PayrollFrequency.SEMI_MONTHLY, [(15, false), (31, true)], new DateOnly(y, m, d));

        period.Should().Be(new DeductionPeriod(new DateOnly(y, m, startDay), new DateOnly(y, m, endDay)));
    }

    [Fact]
    public void Period_Monthly_IsTheWholeCalendarMonth()
    {
        DeductionPeriodResolver.Resolve(PayrollFrequency.MONTHLY, [(31, true)], new DateOnly(2026, 4, 17))
            .Should().Be(new DeductionPeriod(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
    }

    [Fact]
    public void Period_NoGroup_IsTheWholeCalendarMonth()
    {
        DeductionPeriodResolver.Resolve((PayrollGroup?)null, new DateOnly(2026, 4, 17))
            .Should().Be(new DeductionPeriod(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(30)]
    public void Period_Weekly_DaysAfterTheLastCutoffJoinTheLastPeriod(int day)
    {
        // Cutoffs 7/14/21/28: days 29-31 must not form a stray fifth period, or two dates in the
        // same week would resolve to overlapping windows.
        DeductionPeriodResolver.Resolve(
                PayrollFrequency.WEEKLY, [(7, false), (14, false), (21, false), (28, false)], new DateOnly(2026, 1, day))
            .Should().Be(new DeductionPeriod(new DateOnly(2026, 1, 22), new DateOnly(2026, 1, 31)));
    }

    // ---------- DeductionCarrierResolver ----------

    private static DeductionCarrierInput Input(
        Guid? home, IEnumerable<Guid> inRun, Dictionary<Guid, int> days,
        Guid? claimedBy = null, bool carry = false, bool skip = false) =>
        new(Guid.NewGuid(), home, inRun.ToHashSet(), days, claimedBy, carry, skip);

    [Fact]
    public void EveryRun_AlwaysCarries()
    {
        DeductionCarrierResolver.Decide(DeductionCarrierMode.EveryRun,
                Input(ClientA, [ClientB], new() { [ClientA] = 5 }, claimedBy: Guid.NewGuid(), skip: true))
            .Carries.Should().BeTrue("EveryRun is the pre-existing behaviour and ignores every gate");
    }

    [Fact]
    public void AnotherRunsClaim_BeatsEverythingElse()
    {
        DeductionCarrierResolver.Decide(DeductionCarrierMode.PrimaryClient,
                Input(ClientA, [ClientA], new() { [ClientA] = 5 }, claimedBy: Guid.NewGuid(), carry: true))
            .Should().Be(new DeductionCarrierDecision(false, "ClaimedElsewhere"));
    }

    [Fact]
    public void Skip_BeatsCarry()
    {
        DeductionCarrierResolver.Decide(DeductionCarrierMode.Manual, Input(ClientA, [ClientA], new(), carry: true, skip: true))
            .Carries.Should().BeFalse();
    }

    [Fact]
    public void PrimaryClient_CarriesOnlyInTheRunThatPaysTheHomeClient()
    {
        var days = new Dictionary<Guid, int> { [ClientA] = 3, [ClientB] = 9 };

        DeductionCarrierResolver.Decide(DeductionCarrierMode.PrimaryClient, Input(ClientA, [ClientA], days))
            .Carries.Should().BeTrue();
        DeductionCarrierResolver.Decide(DeductionCarrierMode.PrimaryClient, Input(ClientA, [ClientB], days))
            .Carries.Should().BeFalse("they have more days at B, but A is their primary client");
    }

    [Fact]
    public void PrimaryClient_FallsBackToMostDays_WhenTheHomeClientWasNeverWorked()
    {
        var days = new Dictionary<Guid, int> { [ClientB] = 4 };

        var atB = DeductionCarrierResolver.Decide(DeductionCarrierMode.PrimaryClient, Input(ClientA, [ClientB], days));
        atB.Carries.Should().BeTrue("someone has to deduct, and B is where they worked");
        atB.Reason.Should().StartWith("PrimaryClientNotWorked");
    }

    [Fact]
    public void MostDays_PicksTheBusiestClient_AndBreaksTiesOnTheLowestId()
    {
        var days = new Dictionary<Guid, int> { [ClientA] = 5, [ClientB] = 5 };

        DeductionCarrierResolver.Decide(DeductionCarrierMode.MostDays, Input(null, [ClientA], days)).Carries.Should().BeTrue();
        DeductionCarrierResolver.Decide(DeductionCarrierMode.MostDays, Input(null, [ClientB], days)).Carries.Should().BeFalse();
    }

    [Fact]
    public void MostDays_WithNoDaysAtAll_CarriesInWhicheverRunPaysThem()
    {
        // A payroll-only employee has no DTR days to rank.
        DeductionCarrierResolver.Decide(DeductionCarrierMode.MostDays, Input(null, [], new()))
            .Should().Be(new DeductionCarrierDecision(true, "NoDays"));
    }

    [Fact]
    public void Manual_CarriesOnlyWhenTheOperatorChoseThisRun()
    {
        DeductionCarrierResolver.Decide(DeductionCarrierMode.Manual, Input(ClientA, [ClientA], new() { [ClientA] = 5 }))
            .Carries.Should().BeFalse();
        DeductionCarrierResolver.Decide(DeductionCarrierMode.Manual, Input(ClientA, [ClientA], new() { [ClientA] = 5 }, carry: true))
            .Carries.Should().BeTrue();
    }

    [Fact]
    public void FirstRunGenerated_CarriesUntilSomeoneElseHoldsTheClaim()
    {
        DeductionCarrierResolver.Decide(DeductionCarrierMode.FirstRunGenerated, Input(ClientA, [ClientB], new()))
            .Carries.Should().BeTrue();
    }

    // ---------- earnings-only line ----------

    [Fact]
    public void EarningsOnlyLine_TakesHomeTheWholeGross_AndStillSplitsTaxableIncome()
    {
        var line = new PayrollSummaryLine { GrossIncome = 5000m, NonTaxableBenefits = 500m };

        EmployeePayrollLineService.ComputeEarningsOnly(line);

        line.NetPay.Should().Be(5000m);
        line.TotalDeductions.Should().Be(0m);
        line.TaxableIncome.Should().Be(4500m);
        line.NonTaxableIncome.Should().Be(500m);
    }

    // ---------- COLA is paid once per cutoff ----------

    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public void Cola_IsPaidOnlyByTheRunThatCarriesTheDeductions(bool earningsOnly, int expectedCola)
    {
        var context = new Hrms.Core.Pipelines.PayrollContext
        {
            Employee = new EmployeeModelPayrollRun { Id = Guid.NewGuid(), Cola = 500m },
            IsEarningsOnly = earningsOnly,
        };

        var line = new Hrms.Core.Policies.OtherIncome.ColaPolicy().ApplyIfSatisfied(new AllowancePipeData(), context);

        line.Cola.Should().Be(expectedCola);
        line.RunningTotal.Should().Be(expectedCola);
    }

    // ---------- claims (real relational provider: the unique index is the point) ----------

    // SQLite enforces the foreign keys, so every employee needs a real payroll group and client rows.
    private static Guid SeedGroup(HrmsContext seed)
    {
        var id = Guid.NewGuid();
        seed.PayrollGroups.Add(new PayrollGroup { Id = id });
        return id;
    }

    private static void SeedClients(HrmsContext seed)
    {
        seed.Add(new Client { Id = ClientA, Code = "A", Name = "Client A" });
        seed.Add(new Client { Id = ClientB, Code = "B", Name = "Client B" });
    }

    [Fact]
    public async Task Claims_AreStagedAndFound_AndASecondCarrierForTheSamePeriodIsRejected()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Employees.Add(new Employee { Id = employeeId, PayrollGroupId = SeedGroup(seed) });
            await seed.SaveChangesAsync();
        }

        var period = new DeductionPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        var firstRun = Guid.NewGuid();

        await using (var context = db.NewContext())
        {
            var service = new PayrollDeductionClaimService(new UnitOfWorkService(context));
            service.Stage(firstRun, [(employeeId, period)]);
            await service.CommitChangesAsync(CancellationToken.None);
        }

        await using (var context = db.NewContext())
        {
            var service = new PayrollDeductionClaimService(new UnitOfWorkService(context));
            var found = await service.FindClaimsAsync([employeeId], period.Start, period.Start, CancellationToken.None);
            found.Should().ContainSingle().Which.Should().Be(
                new KeyValuePair<(Guid, DateOnly), Guid>((employeeId, period.Start), firstRun));
        }

        await using (var context = db.NewContext())
        {
            var service = new PayrollDeductionClaimService(new UnitOfWorkService(context));
            service.Stage(Guid.NewGuid(), [(employeeId, period)]);
            var second = async () => await service.CommitChangesAsync(CancellationToken.None);
            await second.Should().ThrowAsync<DbUpdateException>("one carrier per employee per period");
        }
    }

    [Fact]
    public async Task Claims_DifferentPeriods_AreIndependent_AndReleaseFreesOnlyThatRun()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Employees.Add(new Employee { Id = employeeId, PayrollGroupId = SeedGroup(seed) });
            await seed.SaveChangesAsync();
        }

        var first =new DeductionPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        var second = new DeductionPeriod(new DateOnly(2026, 1, 16), new DateOnly(2026, 1, 31));
        var runOne = Guid.NewGuid();
        var runTwo = Guid.NewGuid();

        await using var context = db.NewContext();
        var service = new PayrollDeductionClaimService(new UnitOfWorkService(context));
        service.Stage(runOne, [(employeeId, first)]);
        service.Stage(runTwo, [(employeeId, second)]);
        await service.CommitChangesAsync(CancellationToken.None);

        await service.ReleaseByBatchAsync(runOne, CancellationToken.None);

        var left = await service.FindClaimsAsync([employeeId], first.Start, second.Start, CancellationToken.None);
        left.Should().ContainSingle().Which.Key.Should().Be((employeeId, second.Start));
    }

    // ---------- day-based routing ----------

    private static DailyRecordService BuildDailyRecordService(IUnitOfWorkService uow) =>
        new(uow, new TypeAdapterConfig(), Substitute.For<IMapper>(),
            Substitute.For<ILogger<DailyRecordService>>(),
            new LeaveDtrReconciliationService(uow, Substitute.For<ILogger<LeaveDtrReconciliationService>>()),
            new PayrollBatchService(uow), new DTRBatchService(uow),
            new ApprovalEngineService(uow, Substitute.For<IPublishEndpoint>()));

    // A reliever whose own client is A, with approved days at both A (2) and B (1).
    private static Guid SeedReliever(HrmsContext seed)
    {
        SeedClients(seed);
        var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = SeedGroup(seed), ClientId = ClientA };
        seed.Employees.Add(employee);
        DailyRecord Day(int day, Guid client) => new()
        {
            Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDate = new DateOnly(2026, 1, day),
            BatchCode = "DTR", FullName = "Reliever", Posted = true, ClientId = client, RegularNetHours = 8,
        };
        seed.DailyTimeRecords.AddRange(Day(5, ClientA), Day(6, ClientA), Day(7, ClientB));
        return employee.Id;
    }

    [Fact]
    public async Task CutoffRun_ByDayClient_PaysTheDaysWorkedForThatClient_NotTheEmployeesOwnClient()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            SeedReliever(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildDailyRecordService(new UnitOfWorkService(context));
        var range = (From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 1, 15));

        var atB = await service.LoadForPayrollRunByCutoffAsync(
            new CutoffScope(range.From, range.To, null, ClientIds: [ClientB], UseDayClient: true), CancellationToken.None);
        atB.Records.Values.Single().Should().ContainSingle("only the day worked for client B");

        var atA = await service.LoadForPayrollRunByCutoffAsync(
            new CutoffScope(range.From, range.To, null, ClientIds: [ClientA], UseDayClient: true), CancellationToken.None);
        atA.Records.Values.Single().Should().HaveCount(2);
    }

    [Fact]
    public async Task CutoffRun_WithoutDayClient_StillFiltersByTheEmployeesOwnClient()
    {
        using var db = new SqliteHrmsContext();
        await using (var seed = db.NewContext())
        {
            SeedReliever(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildDailyRecordService(new UnitOfWorkService(context));
        var range = (From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 1, 15));

        (await service.LoadForPayrollRunByCutoffAsync(
                new CutoffScope(range.From, range.To, null, ClientIds: [ClientB]), CancellationToken.None))
            .Records.Should().BeEmpty("the employee's own client is A, so filtering on B finds nobody");
        (await service.LoadForPayrollRunByCutoffAsync(
                new CutoffScope(range.From, range.To, null, ClientIds: [ClientA]), CancellationToken.None))
            .Records.Values.Single().Should().HaveCount(3, "every day follows the employee's own client");
    }

    [Fact]
    public async Task Readiness_ByDayClient_CountsOnlyTheDaysWorkedForThatClient()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            employeeId = SeedReliever(seed);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildDailyRecordService(new UnitOfWorkService(context));
        var range = (From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 1, 15));

        var atB = await service.GetPayrollReadinessAsync(
            range.From, range.To, null, CancellationToken.None, [ClientB], useDayClient: true);
        atB.Should().ContainSingle().Which.ApprovedDays.Should().Be(1, "the reliever's own client is A, but one day was worked for B");
        atB[0].EmployeeId.Should().Be(employeeId);

        var everything = await service.GetPayrollReadinessAsync(
            range.From, range.To, null, CancellationToken.None, [ClientB], useDayClient: false);
        everything.Should().ContainSingle().Which.ApprovedDays.Should().Be(3, "without day-client mode the caller narrows by the employee's tag, so the server does not");
    }

    [Fact]
    public async Task Tracker_SplitsWorkedPaidAndUnpaidDaysPerClient_AndSkipsZeroHourDays()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            employeeId = SeedReliever(seed); // A: 2 approved unpaid days, B: 1 approved unpaid day
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 8),
                BatchCode = "DTR", FullName = "Reliever", Posted = true, ClientId = ClientB,
                RegularNetHours = 8, PayrollBatchId = Guid.NewGuid(), // already paid by a run
            });
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 9),
                BatchCode = "DTR", FullName = "Reliever", Posted = true, ClientId = ClientB, // absent
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildDailyRecordService(new UnitOfWorkService(context));

        var rows = await service.GetCutoffTrackerRowsAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), null, CancellationToken.None);

        var row = rows.Should().ContainSingle().Subject;
        row.EmployeeId.Should().Be(employeeId);
        row.HomeClientId.Should().Be(ClientA);
        var a = row.Clients.Single(c => c.ClientId == ClientA);
        (a.WorkedDays, a.UnpaidApprovedDays, a.PaidDays).Should().Be((2, 2, 0));
        var b = row.Clients.Single(c => c.ClientId == ClientB);
        (b.WorkedDays, b.UnpaidApprovedDays, b.PaidDays).Should().Be((2, 1, 1), "the absent day is not a worked day");
    }

    [Fact]
    public async Task CountWorkedDaysByClient_UnpaidOnly_LeavesOutDaysASavedRunPaid()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            employeeId = SeedReliever(seed); // A: 2 unpaid days, B: 1 unpaid day
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 8),
                BatchCode = "DTR", FullName = "Reliever", Posted = true, ClientId = ClientB,
                RegularNetHours = 8, PayrollBatchId = Guid.NewGuid(),
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildDailyRecordService(new UnitOfWorkService(context));
        var window = (From: new DateOnly(2026, 1, 1), To: new DateOnly(2026, 1, 15));

        var unpaid = await service.CountWorkedDaysByClientAsync(
            [employeeId], window.From, window.To, CancellationToken.None, unpaidOnly: true);
        var everything = await service.CountWorkedDaysByClientAsync(
            [employeeId], window.From, window.To, CancellationToken.None);

        unpaid[employeeId].Should().BeEquivalentTo(new Dictionary<Guid, int> { [ClientA] = 2, [ClientB] = 1 });
        everything[employeeId][ClientB].Should().Be(2);
    }

    [Fact]
    public async Task CutoffStatement_ListsEveryRegularRunOfTheCutoff_WithOneSetOfTotals()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = Guid.NewGuid();
        var carrierBatch = Guid.NewGuid();
        var earningsOnlyBatch = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Employees.Add(new Employee { Id = employeeId, PayrollGroupId = SeedGroup(seed) });
            SeedClients(seed);
            Payroll Run(Guid batch, Guid client, bool carrier, decimal gross, decimal deductions, DateOnly end,
                PayrollType type = PayrollType.Regular) => new()
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, PayrollBatchId = batch, ClientId = client,
                PayPeriodStart = new DateOnly(2026, 1, 1), PayPeriodEnd = end, PayrollDate = end,
                IsDeductionCarrier = carrier, GrossIncome = gross, TotalDeductions = deductions,
                NetPay = gross - deductions, FullName = "Reliever", PayrollType = type,
                OtherDeductions = 300, TotalLoans = 100,
            };
            seed.Payrolls.AddRange(
                Run(earningsOnlyBatch, ClientB, false, 3_000, 0, new DateOnly(2026, 1, 15)),
                Run(carrierBatch, ClientA, true, 5_000, 800, new DateOnly(2026, 1, 15)),
                Run(Guid.NewGuid(), ClientA, true, 9_000, 900, new DateOnly(2026, 1, 31)), // other cutoff
                Run(Guid.NewGuid(), ClientA, true, 7_000, 0, new DateOnly(2026, 1, 15), PayrollType.ThirteenthMonth));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new PayrollService(new UnitOfWorkService(context), Substitute.For<IMapper>());

        var statement = await service.GetCutoffStatementAsync(
            employeeId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), CancellationToken.None);

        statement.FullName.Should().Be("Reliever");
        statement.Runs.Should().HaveCount(2, "the other cutoff and the 13th month run are not part of this cutoff");
        statement.Runs.Single(r => r.PayrollBatchId == carrierBatch).IsDeductionCarrier.Should().BeTrue();
        statement.Runs.Single(r => r.PayrollBatchId == earningsOnlyBatch).IsDeductionCarrier.Should().BeFalse();
        statement.Runs.All(r => r.OtherDeductions == 200).Should().BeTrue("other deductions exclude the loans inside them");
        statement.Totals.Should().BeEquivalentTo(new CutoffStatementTotals { GrossIncome = 8_000, TotalDeductions = 800, NetPay = 7_200 });
    }

    // ---------- performance: the number of SQL commands must not grow with headcount ----------

    // Runs every set-based read a per-client run / the tracker makes for `employeeCount` relievers
    // (two clients each) and returns how many SQL commands that took. Stays under 1,000 employees,
    // the size of one IN-list chunk, so a larger count would add chunks by design, not by N+1.
    private static async Task<int> CountQueriesAsync(int employeeCount)
    {
        using var db = new SqliteHrmsContext();
        var ids = new List<Guid>();
        await using (var seed = db.NewContext())
        {
            SeedClients(seed);
            var group = SeedGroup(seed);
            for (var i = 0; i < employeeCount; i++)
            {
                var employee = new Employee { Id = Guid.NewGuid(), PayrollGroupId = group, ClientId = ClientA };
                ids.Add(employee.Id);
                seed.Employees.Add(employee);
                foreach (var (day, client) in new[] { (5, ClientA), (6, ClientB) })
                    seed.DailyTimeRecords.Add(new DailyRecord
                    {
                        Id = Guid.NewGuid(), EmployeeId = employee.Id, WorkDate = new DateOnly(2026, 1, day),
                        BatchCode = "DTR", FullName = "Reliever", Posted = true, ClientId = client, RegularNetHours = 8,
                    });
                seed.PayrollDeductionClaims.Add(new PayrollDeductionClaim
                {
                    EmployeeId = employee.Id, PeriodStart = new DateOnly(2026, 1, 1),
                    PeriodEnd = new DateOnly(2026, 1, 15), PayrollBatchId = Guid.NewGuid(),
                });
            }
            await seed.SaveChangesAsync();
        }

        var counter = new SqlCommandCounter();
        await using var context = db.NewCountingContext(counter);
        var uow = new UnitOfWorkService(context);
        var days = BuildDailyRecordService(uow);
        var claims = new PayrollDeductionClaimService(uow);
        var (from, to) = (new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));
        counter.Reset();

        await days.CountWorkedDaysByClientAsync(ids, from, to, CancellationToken.None);
        await days.CountWorkedDaysByClientAsync(ids, from, to, CancellationToken.None, unpaidOnly: true);
        await claims.FindClaimsAsync(ids, from, from, CancellationToken.None);
        await claims.FindOverlappingAsync(from, to, CancellationToken.None);
        await days.GetCutoffTrackerRowsAsync(from, to, null, CancellationToken.None);
        await days.GetPayrollReadinessAsync(from, to, null, CancellationToken.None, [ClientB], useDayClient: true);

        return counter.Count;
    }

    [Fact]
    public async Task SetBasedReads_RunTheSameNumberOfQueries_WhetherThereAre20EmployeesOr900()
    {
        var small = await CountQueriesAsync(20);
        var large = await CountQueriesAsync(900);

        small.Should().BeGreaterThan(0, "the counter must actually be seeing the commands");
        large.Should().Be(small, "no per-employee query may creep into these reads");
    }

    [Fact]
    public async Task Claims_FindOverlapping_ReturnsOnlyPeriodsTouchingTheWindow()
    {
        using var db = new SqliteHrmsContext();
        var employeeId = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            seed.Employees.Add(new Employee { Id = employeeId, PayrollGroupId = SeedGroup(seed) });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = new PayrollDeductionClaimService(new UnitOfWorkService(context));
        var runOne = Guid.NewGuid();
        service.Stage(runOne, [(employeeId, new DeductionPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15)))]);
        service.Stage(Guid.NewGuid(), [(employeeId, new DeductionPeriod(new DateOnly(2026, 1, 16), new DateOnly(2026, 1, 31)))]);
        await service.CommitChangesAsync(CancellationToken.None);

        var found = await service.FindOverlappingAsync(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 12), CancellationToken.None);

        found.Should().ContainSingle().Which.PayrollBatchId.Should().Be(runOne);
    }

    [Fact]
    public async Task CountWorkedDaysByClient_GroupsTheWholePeriod_AndIgnoresZeroHourDays()
    {
        using var db = new SqliteHrmsContext();
        Guid employeeId;
        await using (var seed = db.NewContext())
        {
            employeeId = SeedReliever(seed);
            seed.DailyTimeRecords.Add(new DailyRecord
            {
                Id = Guid.NewGuid(), EmployeeId = employeeId, WorkDate = new DateOnly(2026, 1, 8),
                BatchCode = "DTR", FullName = "Reliever", Posted = true, ClientId = ClientB, // absent: no hours
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = BuildDailyRecordService(new UnitOfWorkService(context));

        var counts = await service.CountWorkedDaysByClientAsync(
            [employeeId], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), CancellationToken.None);

        counts[employeeId].Should().BeEquivalentTo(new Dictionary<Guid, int> { [ClientA] = 2, [ClientB] = 1 });
    }
}
