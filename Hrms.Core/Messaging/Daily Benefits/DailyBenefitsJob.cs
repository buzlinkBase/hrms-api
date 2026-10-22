using Hrms.Core.Messaging.Filter;
using Hrms.Core.Messaging.LeaveWorkers;
using Hrms.Core.Services;
using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Hrms.Core.Messaging.DailyBenefits;

/// <summary>
/// The daily Quartz job behind leave carry-over/grant/accrual and uniform-allowance accrual
/// (replaces LeaveSchedulerService's hand-rolled "sleep until midnight" loop). Quartz's clustered
/// store makes it fire on exactly one API instance, and runs a missed fire as soon as the API is
/// back.
///
/// Missed days are caught up, not skipped: the last day processed is kept in the database
/// (<see cref="DailyBenefitsRunState"/>), and each run processes every day after it up to the
/// scheduled day (at most <see cref="MaxCatchUpDays"/>). Only days with work -- the 1st of a
/// month -- publish anything: one <see cref="RunDailyBenefits"/> per company, run in order by
/// DailyBenefitsWorker. The date is recorded only after publishing succeeds, so a failed run is
/// retried on the next one.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DailyBenefitsJob : IJob
{
    public static readonly JobKey Key = new("daily-benefits", "hrms");
    public static readonly TriggerKey TriggerKey = new("daily-benefits-trigger", "hrms");
    public const int MaxCatchUpDays = 62;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyBenefitsJob> _logger;

    public DailyBenefitsJob(IServiceScopeFactory scopeFactory, ILogger<DailyBenefitsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var token = context.CancellationToken;
        var runDate = RunDateOf((context.ScheduledFireTimeUtc ?? context.FireTimeUtc).UtcDateTime);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrmsContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var lastRun = await DailyBenefitsRunState.GetLastRunDateAsync(db, token);
        var dates = DatesToRun(lastRun, runDate).Where(HasWork).ToList();
        if (dates.Count > 0)
        {
            var companies = await FindCompaniesAsync(db, token);
            foreach (var date in dates)
            {
                foreach (var (tenantId, fiscalStartMonth) in companies)
                {
                    await publisher.Publish(
                        new RunDailyBenefits(date, fiscalStartMonth),
                        ctx => ctx.Headers.Set(TenantPublishFilter<RunDailyBenefits>.HeaderName, tenantId.ToString()),
                        token);
                }
                _logger.LogInformation("Daily benefits {Date}: queued for {Count} company(ies)", date, companies.Count);
            }
        }

        if (lastRun == null || runDate > lastRun) await DailyBenefitsRunState.SetLastRunDateAsync(db, runDate, token);
        // One save for the run-state row and the outbox: the bus outbox only hands scoped
        // publishes to the broker when this context saves.
        await db.SaveChangesAsync(token);
    }

    /// <summary>The business day a fire belongs to: the Philippine calendar date, like everywhere else in the system.</summary>
    public static DateOnly RunDateOf(DateTime fireTimeUtc) => PhTime.Today(fireTimeUtc);

    /// <summary>Every day after the last one processed, up to and including the run date.</summary>
    public static IEnumerable<DateOnly> DatesToRun(DateOnly? lastRun, DateOnly runDate)
    {
        var from = lastRun?.AddDays(1) ?? runDate;
        var earliest = runDate.AddDays(-MaxCatchUpDays);
        if (from < earliest) from = earliest;
        for (var date = from; date <= runDate; date = date.AddDays(1))
            yield return date;
    }

    // Every day: leave carry-over, grant and accrual fall on the 1st of a month, but a uniform
    // allowance policy can credit on any day of the month or on each employee's anniversary. The
    // steps themselves decide what applies (IDailyBenefitStep.AppliesOn).
    public static bool HasWork(DateOnly date) => true;

    /// <summary>
    /// Every company with a Company record or any employee, with its fiscal-year start month.
    /// Companies used to be found through the Leaves table, so one with no leave types got no
    /// uniform-allowance accrual either.
    /// </summary>
    public static async Task<List<(Guid TenantId, int FiscalYearStartMonth)>> FindCompaniesAsync(HrmsContext db, CancellationToken token)
    {
        var fromCompanies = await db.Companies.IgnoreQueryFilters()
            .Where(x => x.DeletedAt == null && x.TenantId != Guid.Empty)
            .Select(x => x.TenantId)
            .Distinct()
            .ToListAsync(token);
        var fromEmployees = await db.Employees.IgnoreQueryFilters()
            .Where(x => x.DeletedAt == null && x.TenantId != Guid.Empty)
            .Select(x => x.TenantId)
            .Distinct()
            .ToListAsync(token);

        // Each company's own setting -- this used to read whichever company's row came first.
        var fiscalSettings = await db.GeneralSettings.IgnoreQueryFilters()
            .Where(x => x.DeletedAt == null &&
                        x.IdentityType == FiscalYearSettings.IdentityType &&
                        x.Description == FiscalYearSettings.Key)
            .Select(x => new { x.TenantId, x.Value })
            .ToListAsync(token);
        var startMonths = fiscalSettings
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => FiscalYearSettings.Parse(g.First().Value));

        return fromCompanies.Union(fromEmployees)
            .Select(tenantId => (tenantId, startMonths.GetValueOrDefault(tenantId, 1)))
            .ToList();
    }
}

/// <summary>
/// The last day the daily benefits job processed, kept as a system GeneralSetting row (no
/// company). Stored in the database rather than in Quartz's job data, which is rewritten from
/// code on every deploy.
/// </summary>
public static class DailyBenefitsRunState
{
    public const string IdentityType = "System";
    public const string Key = "DailyBenefitsLastRunDate";

    private static IQueryable<GeneralSetting> Rows(HrmsContext db) =>
        db.GeneralSettings.IgnoreQueryFilters()
            .Where(x => x.TenantId == Guid.Empty && x.DeletedAt == null &&
                        x.IdentityType == IdentityType && x.Description == Key);

    public static async Task<DateOnly?> GetLastRunDateAsync(HrmsContext db, CancellationToken token)
    {
        var value = await Rows(db).Select(x => x.Value).FirstOrDefaultAsync(token);
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date) ? date : null;
    }

    /// <summary>Stages the change; the caller's SaveChanges writes it.</summary>
    public static async Task SetLastRunDateAsync(HrmsContext db, DateOnly date, CancellationToken token)
    {
        var value = date.ToString("yyyy-MM-dd");
        var row = await Rows(db).FirstOrDefaultAsync(token);
        if (row == null)
        {
            db.GeneralSettings.Add(new GeneralSetting
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                IdentityType = IdentityType,
                Description = Key,
                Value = value,
            });
        }
        else
        {
            row.Value = value;
        }
    }
}
