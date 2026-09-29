using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

// Runs on the first day of the new fiscal year, before that year's period grant (see
// DailyBenefitsWorker) -- so the closing year's balance is final, not a snapshot taken while its
// last day is still being worked. Applies each leave type's CarryOverType policy to the closing
// year's balances and writes CarryOver/Expiry ledger entries. Carried days go into the next
// year's credit record, which is created if missing or topped up if something (e.g. the accrual
// opener) already created it.
//
// Idempotency: an Expiry entry dated the closing day marks a credit's expiry as done; a CarryOver
// entry dated the next period's first day marks that employee+leave's carry as done. A re-run
// skips whatever is already done. Duplicate-key exceptions are the last resort.
public class LeaveCarryOverWorker : IConsumer<RunLeaveCarryOver>
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeaveCarryOverWorker> _logger;

    public LeaveCarryOverWorker(IUnitOfWorkService uow, ILogger<LeaveCarryOverWorker> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<RunLeaveCarryOver> context) =>
        RunAsync(context.Message, context.CancellationToken);

    public async Task RunAsync(RunLeaveCarryOver message, CancellationToken token)
    {
        var fromYear         = message.FromYear;
        var fiscalStartMonth = message.FiscalYearStartMonth;
        var nextYear         = fromYear + 1;
        var closeDate        = FiscalYearHelper.LastDayOfFiscalYear(fromYear, fiscalStartMonth);
        var nextStart        = FiscalYearHelper.PeriodStart(nextYear, fiscalStartMonth);
        var nextEnd          = FiscalYearHelper.PeriodEnd(nextYear, fiscalStartMonth);
        var nextStartDate    = DateOnly.FromDateTime(nextStart);
        var carryCount  = 0;
        var expiryCount = 0;

        var closingCredits = await _uow.Repository
            .Find<LeaveCredits>(x => x.PeriodYear == fromYear)
            .Include(x => x.Leave)
            .ToListAsync(token);

        // Credits whose closing-year expiry was already written (exact closeDate, so
        // non-calendar fiscal years are matched correctly).
        var alreadyExpiredIds = await _uow.Repository
            .Find<LeaveLedger>(x => x.EntryType == LedgerEntryType.Expiry && x.EntryDate == closeDate)
            .Select(x => x.LeaveCreditsId)
            .ToHashSetAsync(token);

        // Employee+leave pairs whose carry into the next year was already written.
        var alreadyCarried = (await _uow.Repository
            .Find<LeaveLedger>(x => x.EntryType == LedgerEntryType.CarryOver && x.EntryDate == nextStartDate)
            .Select(x => new { x.EmployeeId, x.LeaveId })
            .ToListAsync(token))
            .Select(k => (k.EmployeeId, k.LeaveId))
            .ToHashSet();

        // Next-year rows that already exist -- carried days are added to these rather than lost.
        var nextYearRows = (await _uow.Repository
            .Find<LeaveCredits>(x => x.PeriodYear == nextYear)
            .ToListAsync(token))
            .ToDictionary(x => (x.EmployeeId, x.LeaveId));

        var newCredits = new List<LeaveCredits>();
        var newLedgers = new List<LeaveLedger>();

        foreach (var credit in closingCredits)
        {
            var leave = credit.Leave;
            if (leave == null) continue;

            // PerEvent leaves don't carry over — each event is independent
            if (leave.AccrualBasis == AccrualBasis.PerEvent) continue;

            var key          = (credit.EmployeeId, credit.LeaveId);
            var expiryDone   = alreadyExpiredIds.Contains(credit.Id);
            var carryDone    = alreadyCarried.Contains(key);
            if (expiryDone && carryDone) continue;

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

            if (expiryAmount > 0 && !expiryDone)
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
                    Amount         = -expiryAmount,
                    Balance        = credit.Balance,
                    Particulars    = $"Year-end expiry — {leave.Description} {fromYear}",
                });
                expiryCount++;
            }

            if (carryAmount > 0 && !carryDone)
            {
                if (!nextYearRows.TryGetValue(key, out var nextCredits))
                {
                    nextCredits = new LeaveCredits
                    {
                        Id         = Guid.NewGuid(),
                        EmployeeId = credit.EmployeeId,
                        LeaveId    = credit.LeaveId,
                        PeriodYear = nextYear,
                        FromDate   = nextStart,
                        ToDate     = nextEnd,
                        Granted    = 0m,
                        Used       = 0m,
                        Balance    = 0m,
                    };
                    newCredits.Add(nextCredits);
                    nextYearRows[key] = nextCredits;
                }
                nextCredits.Granted += carryAmount;
                nextCredits.Balance += carryAmount;

                newLedgers.Add(new LeaveLedger
                {
                    Id             = Guid.NewGuid(),
                    EmployeeId     = credit.EmployeeId,
                    LeaveId        = credit.LeaveId,
                    LeaveCreditsId = nextCredits.Id,
                    EntryType      = LedgerEntryType.CarryOver,
                    EntryDate      = nextStartDate,
                    Amount         = carryAmount,
                    Balance        = nextCredits.Balance,
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
