using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Messaging.DailyBenefits;
using Hrms.Core.Messaging.LeaveWorkers;
using Hrms.Core.Services.UniformAllowance;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using hrms.test.TestSupport;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Onepunch.Common.Lib;
using System.Runtime.CompilerServices;

namespace hrms.test.MessagingTests;

/// <summary>
/// The Quartz daily benefits run that replaced LeaveSchedulerService: which days it processes
/// (including catch-up), which companies it finds (and their own fiscal years), and that the
/// steps run in order, each in its own scope for the right company.
/// </summary>
public class DailyBenefitsTests
{
    // ── Days to process ─────────────────────────────────────────────────────

    [Fact]
    public void FirstEverRun_ProcessesOnlyItsOwnDay()
    {
        DailyBenefitsJob.DatesToRun(null, new DateOnly(2026, 10, 1)).Should().Equal(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void MissedDays_AreCaughtUp_FromTheDayAfterTheLastRun()
    {
        var days = DailyBenefitsJob.DatesToRun(new DateOnly(2026, 9, 29), new DateOnly(2026, 10, 2)).ToList();

        days.Should().Equal(new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2));
        // Every day has work now: a uniform allowance policy can credit on any day.
        days.Where(DailyBenefitsJob.HasWork).Should().Equal(days);
    }

    [Fact]
    public void AlreadyProcessedDay_IsNotRunAgain()
    {
        DailyBenefitsJob.DatesToRun(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)).Should().BeEmpty();
    }

    [Fact]
    public void CatchUp_IsCapped()
    {
        var days = DailyBenefitsJob.DatesToRun(new DateOnly(2025, 1, 1), new DateOnly(2026, 10, 1)).ToList();

        days.Should().HaveCount(DailyBenefitsJob.MaxCatchUpDays + 1);
        days.Last().Should().Be(new DateOnly(2026, 10, 1));
    }

    // ── Philippine time ─────────────────────────────────────────────────────

    [Fact]
    public void RunDate_IsThePhilippineCalendarDay_NotTheUtcOne()
    {
        // 16:00 UTC is midnight in Manila: already the next day there.
        DailyBenefitsJob.RunDateOf(new DateTime(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2026, 10, 1));
        DailyBenefitsJob.RunDateOf(new DateTime(2026, 9, 30, 15, 59, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Midnight_Manila_Cron_FiresAt1600Utc()
    {
        var cron = new Quartz.CronExpression("0 0 0 * * ?") { TimeZone = Hrms.Core.Services.PhTime.Zone };

        var next = cron.GetNextValidTimeAfter(new DateTimeOffset(2026, 9, 30, 15, 59, 0, TimeSpan.Zero));

        next.Should().Be(new DateTimeOffset(2026, 9, 30, 16, 0, 0, TimeSpan.Zero));
        DailyBenefitsJob.RunDateOf(next!.Value.UtcDateTime).Should().Be(new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void FirstManilaRun_AfterAUtcEraRun_DoesNotRepeatThatDay()
    {
        // The last UTC-midnight run (00:00 UTC Oct 1 = 08:00 Manila) recorded Oct 1; the first Manila
        // midnight after it is Oct 2 00:00 Manila (16:00 UTC Oct 1), so only Oct 2 is processed.
        var runDate = DailyBenefitsJob.RunDateOf(new DateTime(2026, 10, 1, 16, 0, 0, DateTimeKind.Utc));

        DailyBenefitsJob.DatesToRun(new DateOnly(2026, 10, 1), runDate).Should().Equal(new DateOnly(2026, 10, 2));
    }

    // ── Companies ───────────────────────────────────────────────────────────

    [Fact]
    public async Task FindsEveryCompany_WithItsOwnFiscalYearStart()
    {
        using var db = new SqliteHrmsContext();
        var withCompanyRow = Guid.NewGuid();
        var employeesOnly = Guid.NewGuid();
        var aprilFiscal = Guid.NewGuid();
        await using (var seed = db.NewContext())
        {
            var payrollGroup = new PayrollGroup { Id = Guid.NewGuid() };
            seed.PayrollGroups.Add(payrollGroup);
            seed.Companies.Add(new Company { Id = Guid.NewGuid(), TenantId = withCompanyRow, Code = "A" });
            seed.Companies.Add(new Company { Id = Guid.NewGuid(), TenantId = aprilFiscal, Code = "C" });
            // No Company row and no leave types -- used to be invisible to the scheduler.
            seed.Employees.Add(new Employee { Id = Guid.NewGuid(), TenantId = employeesOnly, PayrollGroupId = payrollGroup.Id });
            seed.GeneralSettings.Add(new GeneralSetting
            {
                Id = Guid.NewGuid(), TenantId = aprilFiscal,
                IdentityType = FiscalYearSettings.IdentityType, Description = FiscalYearSettings.Key, Value = "4",
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var companies = await DailyBenefitsJob.FindCompaniesAsync(context, CancellationToken.None);

        companies.Should().BeEquivalentTo(new[]
        {
            (withCompanyRow, 1),
            (employeesOnly, 1),
            (aprilFiscal, 4),
        });
    }

    [Fact]
    public async Task LastRunDate_IsKeptInTheDatabase()
    {
        using var db = new SqliteHrmsContext();
        await using (var context = db.NewContext())
        {
            (await DailyBenefitsRunState.GetLastRunDateAsync(context, CancellationToken.None)).Should().BeNull();
            await DailyBenefitsRunState.SetLastRunDateAsync(context, new DateOnly(2026, 10, 1), CancellationToken.None);
            await context.SaveChangesAsync();
        }
        await using (var context = db.NewContext())
        {
            await DailyBenefitsRunState.SetLastRunDateAsync(context, new DateOnly(2026, 10, 2), CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var verify = db.NewContext();
        (await DailyBenefitsRunState.GetLastRunDateAsync(verify, CancellationToken.None)).Should().Be(new DateOnly(2026, 10, 2));
        (await verify.GeneralSettings.IgnoreQueryFilters().CountAsync(x => x.Description == DailyBenefitsRunState.Key)).Should().Be(1);
    }

    // ── Steps ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 1, 1, 1, true, true)]    // calendar fiscal year starts: everything
    [InlineData(2026, 3, 1, 1, false, true)]   // ordinary 1st: accruals only
    [InlineData(2026, 4, 1, 4, true, true)]    // April fiscal year starts
    [InlineData(2026, 3, 15, 1, false, false)] // mid-month: no leave steps
    public void StepsApply_OnTheRightDays(int y, int m, int d, int fiscalStart, bool yearEndSteps, bool monthlySteps)
    {
        var run = new RunDailyBenefits(new DateOnly(y, m, d), fiscalStart);

        new LeaveCarryOverStep().AppliesOn(run).Should().Be(yearEndSteps);
        new LeavePeriodGrantStep().AppliesOn(run).Should().Be(yearEndSteps);
        new AccrualCreditOpeningStep().AppliesOn(run).Should().Be(monthlySteps);
        new LeaveAccrualStep().AppliesOn(run).Should().Be(monthlySteps);
        new UniformAllowanceAccrualStep().AppliesOn(run).Should().BeTrue("each policy picks its own credit day");
    }

    [Fact]
    public void Steps_RunCarryOverBeforeGrant_AndOpeningBeforeAccrual()
    {
        var services = new ServiceCollection().AddDailyBenefits().BuildServiceProvider();
        var order = services.GetServices<IDailyBenefitStep>().OrderBy(s => s.Order).Select(s => s.GetType()).ToList();

        order.Should().Equal(
            typeof(LeaveCarryOverStep), typeof(LeavePeriodGrantStep), typeof(AccrualCreditOpeningStep),
            typeof(LeaveAccrualStep), typeof(UniformAllowanceAccrualStep));
    }

    [Fact]
    public async Task Worker_RunsApplicableStepsInOrder_EachInItsOwnScopeForTheMessagesCompany()
    {
        var tenantId = Guid.NewGuid();
        var calls = new List<(string Step, Guid Tenant, int Scope)>();
        var services = new ServiceCollection()
            .AddScoped<ITenantProvider, FakeTenantProvider>()
            .AddScoped<TenantConnectionStringInfo>()
            .BuildServiceProvider();
        var steps = new IDailyBenefitStep[]
        {
            new RecordingStep("accrual", 40, run => run.Date.Day == 1, calls),
            new RecordingStep("carry", 10, run => run.Date.Month == 1, calls),
            new RecordingStep("never", 5, _ => false, calls),
        };
        var consumeTenant = new FakeTenantProvider();
        consumeTenant.SetTenantId(tenantId);
        var worker = new DailyBenefitsWorker(
            services.GetRequiredService<IServiceScopeFactory>(), consumeTenant,
            new TenantConnectionStringInfo { TenantId = tenantId }, steps, NullLogger<DailyBenefitsWorker>.Instance);
        var context = Substitute.For<ConsumeContext<RunDailyBenefits>>();
        context.Message.Returns(new RunDailyBenefits(new DateOnly(2026, 1, 1), 1));

        await worker.Consume(context);

        calls.Select(c => c.Step).Should().Equal("carry", "accrual");
        calls.Should().OnlyContain(c => c.Tenant == tenantId);
        calls.Select(c => c.Scope).Distinct().Should().HaveCount(2, "each step gets a fresh scope");
    }

    [Fact]
    public void UniformAllowance_IsJudgedOnTheMonthThatJustEnded()
    {
        // Regression: this used to be the 1st..the 1st of the current month -- one day, so a
        // Present-Days client's employees could never reach the threshold.
        var monthly = UniformCreditSchedules.For(UniformCreditFrequency.Monthly);
        var policy = UniformPolicySnapshot.FromLegacy(new Client { UniformAllowance = 1m })!;
        monthly.PeriodFor(new DateOnly(2026, 3, 1), policy)
            .Should().Be((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)));
        monthly.PeriodFor(new DateOnly(2026, 1, 1), policy)
            .Should().Be((new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 31)));
    }

    private sealed class FakeTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; private set; }
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class RecordingStep(string name, int order, Func<RunDailyBenefits, bool> applies, List<(string, Guid, int)> calls)
        : IDailyBenefitStep
    {
        public int Order => order;
        public string Name => name;
        public bool AppliesOn(RunDailyBenefits run) => applies(run);

        public Task RunAsync(IServiceProvider services, RunDailyBenefits run, CancellationToken token)
        {
            calls.Add((name, services.GetRequiredService<ITenantProvider>().TenantId,
                RuntimeHelpers.GetHashCode(services.GetRequiredService<TenantConnectionStringInfo>())));
            return Task.CompletedTask;
        }
    }
}
