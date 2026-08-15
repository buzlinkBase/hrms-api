using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

// Runs on Jan 1. Creates LeaveCredits records for the new year for all eligible employees
// on leave types with AccrualBasis.None (lump-sum) that don't already have a current-year record.
//
// Idempotency: pre-checks the existing year set then catches any unique-constraint violation
// from a concurrent run that slipped through the pre-check window.
public class LeavePeriodGrantWorker : IConsumer<RunLeavePeriodGrant>
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeavePeriodGrantWorker> _logger;

    public LeavePeriodGrantWorker(IUnitOfWorkService uow, ILogger<LeavePeriodGrantWorker> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunLeavePeriodGrant> context)
    {
        var year        = context.Message.Year;
        var token       = context.CancellationToken;
        var periodStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd   = new DateTime(year, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var entryDate   = DateOnly.FromDateTime(periodStart);
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
            .ToListAsync(token);

        // Load existing year keys in one query — the primary idempotency guard
        var existingSet = (await _uow.Repository
            .Find<LeaveCredits>(x => x.PeriodYear == year)
            .AsNoTracking()
            .Select(x => new { x.EmployeeId, x.LeaveId })
            .ToListAsync(token))
            .Select(k => (k.EmployeeId, k.LeaveId))
            .ToHashSet();

        var newCredits = new List<LeaveCredits>();
        var newLedgers = new List<LeaveLedger>();

        foreach (var leave in leaves)
        {
            foreach (var emp in activeEmployees)
            {
                var serviceMonths = MonthsBetween(emp.HireDate.ToDateTime(TimeOnly.MinValue), periodStart);
                if (serviceMonths < leave.MinServiceMonths) continue;

                if (existingSet.Contains((emp.Id, leave.Id))) continue;

                var credits = new LeaveCredits
                {
                    Id         = Guid.NewGuid(),
                    EmployeeId = emp.Id,
                    LeaveId    = leave.Id,
                    PeriodYear = year,
                    FromDate   = periodStart,
                    ToDate     = periodEnd,
                    Granted    = (decimal)leave.Credits,
                    Used       = 0m,
                    Balance    = (decimal)leave.Credits,
                };

                newCredits.Add(credits);
                newLedgers.Add(new LeaveLedger
                {
                    Id             = Guid.NewGuid(),
                    EmployeeId     = emp.Id,
                    LeaveId        = leave.Id,
                    LeaveCreditsId = credits.Id,
                    EntryType      = LedgerEntryType.Grant,
                    EntryDate      = entryDate,
                    Add            = credits.Granted,
                    Less           = 0m,
                    Balance        = credits.Balance,
                    Particulars    = $"Period grant — {leave.Description} {year}",
                });

                count++;
            }
        }

        if (newCredits.Count == 0) return;

        _uow.Repository.AddRange(newCredits);
        _uow.Repository.AddRange(newLedgers);

        try
        {
            await _uow.CommitChangesAsync("",token);
            _logger.LogInformation("Period grant {Year}: created {Count} credit record(s)", year, count);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Concurrent run already inserted some of these — treat as success
            _logger.LogWarning(
                "Period grant {Year}: duplicate entries detected (concurrent run). Already committed by another instance.",
                year);
        }
    }

    private static int MonthsBetween(DateTime from, DateTime to) =>
        (to.Year - from.Year) * 12 + (to.Month - from.Month);

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
