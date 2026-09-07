using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

// Runs on Dec 31. Applies each leave type's CarryOverType policy to the closing year's
// balances, writes CarryOver/Expiry ledger entries, and creates the next year's credit records.
//
// Idempotency: pre-checks for existing next-year credit records and existing Expiry ledger
// entries to skip employees already processed. Catches duplicate-key exceptions as a last resort.
public class LeaveCarryOverWorker : IConsumer<RunLeaveCarryOver>
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeaveCarryOverWorker> _logger;

    public LeaveCarryOverWorker(IUnitOfWorkService uow, ILogger<LeaveCarryOverWorker> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunLeaveCarryOver> context)
    {
        var fromYear         = context.Message.FromYear;
        var fiscalStartMonth = context.Message.FiscalYearStartMonth;
        var nextYear         = fromYear + 1;
        var token            = context.CancellationToken;
        var closeDate        = FiscalYearHelper.LastDayOfFiscalYear(fromYear, fiscalStartMonth);
        var nextStart        = FiscalYearHelper.PeriodStart(nextYear, fiscalStartMonth);
        var nextEnd          = FiscalYearHelper.PeriodEnd(nextYear, fiscalStartMonth);
        var carryCount  = 0;
        var expiryCount = 0;

        var closingCredits = await _uow.Repository
            .Find<LeaveCredits>(x => x.PeriodYear == fromYear)
            .Include(x => x.Leave)
            .ToListAsync(token);

        // Pre-check: which credit records were already processed (have an Expiry or CarryOver ledger entry)?
        // Use the exact closeDate so non-calendar fiscal years (end month ≠ Dec) are matched correctly.
        var alreadyProcessedIds = await _uow.Repository
            .Find<LeaveLedger>(x =>
                (x.EntryType == LedgerEntryType.Expiry || x.EntryType == LedgerEntryType.CarryOver) &&
                x.EntryDate == closeDate)
            .Select(x => x.LeaveCreditsId)
            .ToHashSetAsync(token);

        // Pre-check: next-year credit records already created
        var nextYearSet = (await _uow.Repository
            .Find<LeaveCredits>(x => x.PeriodYear == nextYear)
            .AsNoTracking()
            .Select(x => new { x.EmployeeId, x.LeaveId })
            .ToListAsync(token))
            .Select(k => (k.EmployeeId, k.LeaveId))
            .ToHashSet();

        var newCredits = new List<LeaveCredits>();
        var newLedgers = new List<LeaveLedger>();

        foreach (var credit in closingCredits)
        {
            var leave = credit.Leave;
            if (leave == null) continue;

            // PerEvent leaves don't carry over — each event is independent
            if (leave.AccrualBasis == AccrualBasis.PerEvent) continue;

            // Skip if already processed on a previous (retried) run
            if (alreadyProcessedIds.Contains(credit.Id)) continue;

            var remaining    = credit.Balance;
            decimal carryAmount  = 0m;
            decimal expiryAmount = 0m;

            switch (leave.CarryOverType)
            {
                case CarryOverType.Forfeit:
                    expiryAmount = remaining;
                    break;

                case CarryOverType.Unlimited:
                    carryAmount = remaining;
                    break;

                case CarryOverType.Capped:
                    carryAmount  = Math.Min(remaining, (decimal)leave.CarryOverMaxDays);
                    expiryAmount = remaining - carryAmount;
                    break;
            }

            if (expiryAmount > 0)
            {
                credit.Balance -= expiryAmount;
                newLedgers.Add(new LeaveLedger
                {
                    Id             = Guid.NewGuid(),
                    EmployeeId     = credit.EmployeeId,
                    LeaveId        = credit.LeaveId,
                    LeaveCreditsId = credit.Id,
                    EntryType      = LedgerEntryType.Expiry,
                    EntryDate      = closeDate,
                    Add            = 0m,
                    Less           = expiryAmount,
                    Balance        = credit.Balance,
                    Particulars    = $"Year-end expiry — {leave.Description} {fromYear}",
                });
                expiryCount++;
            }

            if (carryAmount > 0 && !nextYearSet.Contains((credit.EmployeeId, credit.LeaveId)))
            {
                var nextCredits = new LeaveCredits
                {
                    Id         = Guid.NewGuid(),
                    EmployeeId = credit.EmployeeId,
                    LeaveId    = credit.LeaveId,
                    PeriodYear = nextYear,
                    FromDate   = nextStart,
                    ToDate     = nextEnd,
                    Granted    = carryAmount,
                    Used       = 0m,
                    Balance    = carryAmount,
                };
                newCredits.Add(nextCredits);

                newLedgers.Add(new LeaveLedger
                {
                    Id             = Guid.NewGuid(),
                    EmployeeId     = credit.EmployeeId,
                    LeaveId        = credit.LeaveId,
                    LeaveCreditsId = nextCredits.Id,
                    EntryType      = LedgerEntryType.CarryOver,
                    EntryDate      = DateOnly.FromDateTime(nextStart),
                    Add            = carryAmount,
                    Less           = 0m,
                    Balance        = carryAmount,
                    Particulars    = $"Carry-over from {fromYear} — {leave.Description}",
                });
                carryCount++;
            }
        }

        if (newCredits.Count == 0 && newLedgers.Count == 0) return;

        _uow.Repository.AddRange(newCredits);
        _uow.Repository.AddRange(newLedgers);

        try
        {
            await _uow.CommitChangesAsync("",token);
            _logger.LogInformation(
                "Carry-over FY{FromYear}→FY{NextYear} (startMonth={Month}): {Carry} carried, {Expiry} expired",
                fromYear, nextYear, fiscalStartMonth, carryCount, expiryCount);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            _logger.LogWarning(
                "Carry-over {FromYear}: duplicate entries detected (concurrent run). Already committed by another instance.",
                fromYear);
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)    == true;
}

public class LeaveCarryOverWorkerDefinition : ConsumerDefinition<LeaveCarryOverWorker>
{
    public LeaveCarryOverWorkerDefinition()
    {
        EndpointName           = "hrms-leave-carry-over-que";
        ConcurrentMessageLimit = 1;
    }
}
