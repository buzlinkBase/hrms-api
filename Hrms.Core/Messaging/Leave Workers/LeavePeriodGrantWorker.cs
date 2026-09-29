using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

// Runs on the first day of the fiscal year, right after the carry-over (see DailyBenefitsWorker).
// Grants the year's lump-sum credits for every eligible employee on leave types with
// AccrualBasis.None.
//
// A record that already exists for the year only gets the grant added when the system opened it
// (e.g. carry-over put last year's unused days there) -- it had no Grant and no manual HR
// Adjustment yet. A record that was already granted, or that HR set by hand, is left alone.
//
// Idempotency: that "already granted" check is the guard; a unique-constraint violation from a
// concurrent run that slipped past it is the last resort.
public class LeavePeriodGrantWorker : IConsumer<RunLeavePeriodGrant>
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeavePeriodGrantWorker> _logger;
    private readonly DailyRecordService _dailyRecordService;

    public LeavePeriodGrantWorker(IUnitOfWorkService uow, ILogger<LeavePeriodGrantWorker> logger, DailyRecordService dailyRecordService)
    {
        _uow    = uow;
        _logger = logger;
        _dailyRecordService = dailyRecordService;
    }

    public Task Consume(ConsumeContext<RunLeavePeriodGrant> context) =>
        RunAsync(context.Message, context.CancellationToken);

    public async Task RunAsync(RunLeavePeriodGrant message, CancellationToken token)
    {
        var year             = message.Year;
        var fiscalStartMonth = message.FiscalYearStartMonth;
        var periodStart      = FiscalYearHelper.PeriodStart(year, fiscalStartMonth);
        var periodEnd        = FiscalYearHelper.PeriodEnd(year, fiscalStartMonth);
        var entryDate        = DateOnly.FromDateTime(periodStart);
        var count       = 0;

        // Only lump-sum leave types; PerEvent credits are granted at filing time
        var leaves = await _uow.Repository
            .Find<Leave>(x => x.AccrualBasis == AccrualBasis.None)
            .AsNoTracking()
            .ToListAsync(token);

        if (leaves.Count == 0) return;

        var activeEmployees = await _uow.Repository
            .Find<Employee>(x =>
                x.EmploymentStatus == EmploymentStatus.Regular      ||
                x.EmploymentStatus == EmploymentStatus.Probationary ||
                x.EmploymentStatus == EmploymentStatus.Contract)
            .AsNoTracking()
            // Only what the eligibility check needs -- not every column of every active employee.
            .Select(x => new { x.Id, x.HireDate })
            .ToListAsync(token);

        // This year's existing records, and which of them are already granted or HR-managed.
        var existingRows = (await _uow.Repository
            .Find<LeaveCredits>(x => x.PeriodYear == year)
            .ToListAsync(token))
            .ToDictionary(x => (x.EmployeeId, x.LeaveId));
        var existingRowIds = existingRows.Values.Select(x => x.Id).ToList();
        var settledRowIds = existingRowIds.Count == 0
            ? new HashSet<Guid>()
            : await _uow.Repository
                .Find<LeaveLedger>(x =>
                    existingRowIds.Contains(x.LeaveCreditsId) &&
                    (x.EntryType == LedgerEntryType.Grant || x.EntryType == LedgerEntryType.Adjustment))
                .Select(x => x.LeaveCreditsId)
                .ToHashSetAsync(token);

        // Only queried when at least one leave type actually uses the PresentDays basis — one
        // grouped query up front rather than one round-trip per employee inside the loop below.
        var periodStartDate = DateOnly.FromDateTime(periodStart);
        var presentDaysByEmployee = leaves.Any(l => l.EligibilityBasis == LeaveEligibilityBasis.PresentDays)
            ? await _dailyRecordService.CountPresentDaysBatchAsync(activeEmployees.Select(e => e.Id), periodStartDate, token)
            : new Dictionary<Guid, int>();

        var newCredits = new List<LeaveCredits>();
        var newLedgers = new List<LeaveLedger>();

        foreach (var leave in leaves)
        {
            foreach (var emp in activeEmployees)
            {
                var monthsServed = LeaveEligibilityCalculator.MonthsBetween(emp.HireDate, periodStartDate);
                var presentDays = presentDaysByEmployee.GetValueOrDefault(emp.Id);
                if (!LeaveEligibilityCalculator.IsServiceRequirementMet(leave, monthsServed, presentDays)) continue;

                var grant = (decimal)leave.Credits;
                if (existingRows.TryGetValue((emp.Id, leave.Id), out var credits))
                {
                    if (settledRowIds.Contains(credits.Id)) continue;
                    credits.Granted += grant;
                    credits.Balance += grant;
                }
                else
                {
                    credits = new LeaveCredits
                    {
                        Id         = Guid.NewGuid(),
                        EmployeeId = emp.Id,
                        LeaveId    = leave.Id,
                        PeriodYear = year,
                        FromDate   = periodStart,
                        ToDate     = periodEnd,
                        Granted    = grant,
                        Used       = 0m,
                        Balance    = grant,
                    };
                    newCredits.Add(credits);
                }

                newLedgers.Add(new LeaveLedger
                {
                    Id             = Guid.NewGuid(),
                    EmployeeId     = emp.Id,
                    LeaveId        = leave.Id,
                    LeaveCreditsId = credits.Id,
                    EntryType      = LedgerEntryType.Grant,
                    EntryDate      = entryDate,
                    Amount         = grant,
                    Balance        = credits.Balance,
                    Particulars    = $"Period grant — {leave.Description} {year}",
                });

                count++;
            }
        }

        if (newLedgers.Count == 0) return;

        _uow.Repository.AddRange(newCredits);
        _uow.Repository.AddRange(newLedgers);

        try
        {
            await _uow.CommitChangesAsync("",token);
            _logger.LogInformation(
                "Period grant FY{Year} (startMonth={Month}): created {Count} credit record(s)",
                year, fiscalStartMonth, count);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Concurrent run already inserted some of these — treat as success
            _logger.LogWarning(
                "Period grant {Year}: duplicate entries detected (concurrent run). Already committed by another instance.",
                year);
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)    == true;
}

public class LeavePeriodGrantWorkerDefinition : ConsumerDefinition<LeavePeriodGrantWorker>
{
    public LeavePeriodGrantWorkerDefinition()
    {
        EndpointName           = "hrms-leave-period-grant-que";
        ConcurrentMessageLimit = 1;
    }
}
