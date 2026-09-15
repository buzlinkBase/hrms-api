using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

// Runs on the 1st of each month. Processes Monthly accrual for all active LeaveCredits records.
// Also handles Annually accrual on Jan 1.
//
// Idempotency: before accruing each credit record, checks whether an Accrual ledger entry
// already exists for the same year+month (fast pre-check, avoids most redelivery work
// entirely). LeaveLedger.AccrualDedupeKey (a unique index, populated only for Accrual entries)
// is the hard stop if two messages race past the pre-check -- the same two-layer pattern
// LeaveGrantOnEventWorker already uses. Each credit is committed individually (not batched into
// one SaveChanges for the whole run) specifically so a duplicate-key collision on one credit
// can never roll back another employee's legitimate accrual in the same run -- this worker only
// fires once a month, so the extra round-trips are a non-issue.
public class LeaveAccrualWorker : IConsumer<RunLeaveAccrual>
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeaveAccrualWorker> _logger;

    public LeaveAccrualWorker(IUnitOfWorkService uow, ILogger<LeaveAccrualWorker> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunLeaveAccrual> context)
    {
        var processDate      = context.Message.ProcessDate;
        var fiscalStartMonth = context.Message.FiscalYearStartMonth;
        var token            = context.CancellationToken;
        var today            = processDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var count            = 0;

        var accrualLeaves = await _uow.Repository
            .Find<Leave>(x =>
                x.AccrualBasis == AccrualBasis.Monthly     ||
                x.AccrualBasis == AccrualBasis.Annually    ||
                x.AccrualBasis == AccrualBasis.PerPayPeriod)
            .AsNoTracking()
            .ToListAsync(token);

        // Load the credit IDs that already have an accrual entry for this exact year+month
        // in a single query to avoid N+1 checks inside the loop
        var alreadyAccruedIds = await _uow.Repository
            .Find<LeaveLedger>(x =>
                x.EntryType      == LedgerEntryType.Accrual &&
                x.EntryDate.Year == processDate.Year        &&
                x.EntryDate.Month == processDate.Month)
            .Select(x => x.LeaveCreditsId)
            .ToHashSetAsync(token);

        foreach (var leave in accrualLeaves)
        {
            if (leave.AccrualBasis == AccrualBasis.Annually &&
                !FiscalYearHelper.IsFiscalYearStart(processDate, fiscalStartMonth))
                continue;

            var accrualRate = (decimal)leave.AccrualRate;
            if (accrualRate <= 0) continue;

            var credits = await _uow.Repository
                .Find<LeaveCredits>(x => x.LeaveId == leave.Id && x.FromDate <= today && x.ToDate >= today)
                .ToListAsync(token);

            foreach (var credit in credits)
            {
                // Skip if this credit record was already accrued this month (idempotency guard)
                if (alreadyAccruedIds.Contains(credit.Id)) continue;

                var uncapped    = credit.Balance + accrualRate;
                var newBalance  = leave.MaxAccrualBalance.HasValue
                    ? Math.Min(uncapped, (decimal)leave.MaxAccrualBalance.Value)
                    : uncapped;
                var earned      = newBalance - credit.Balance;

                if (earned <= 0) continue;

                credit.Granted += earned;
                credit.Balance  = newBalance;

                var ledgerEntry = new LeaveLedger
                {
                    Id                = Guid.NewGuid(),
                    EmployeeId        = credit.EmployeeId,
                    LeaveId           = leave.Id,
                    LeaveCreditsId    = credit.Id,
                    EntryType         = LedgerEntryType.Accrual,
                    EntryDate         = processDate,
                    Add               = earned,
                    Less              = 0m,
                    Balance           = credit.Balance,
                    Particulars       = $"{leave.AccrualBasis} accrual — {processDate:MMM yyyy}",
                    AccrualDedupeKey  = $"{credit.Id}|{processDate:yyyy-MM}",
                };
                _uow.Repository.Add(ledgerEntry);

                try
                {
                    await _uow.CommitChangesAsync("", token);
                    count++;
                }
                catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
                {
                    // Another instance/redelivery already accrued this credit this month —
                    // safe to ignore. Revert the in-memory mutation and detach the failed
                    // ledger insert -- otherwise EF's change tracker would keep retrying both
                    // on the NEXT credit's commit (a single DbContext lives for this whole
                    // Consume call), poisoning every remaining credit in this run too.
                    _logger.LogWarning(
                        "Duplicate accrual skipped for credit {CreditId} ({Date}) — already committed by another instance/redelivery",
                        credit.Id, processDate);
                    credit.Granted -= earned;
                    credit.Balance -= earned;
                    _uow.Context.Entry(credit).State = EntityState.Unchanged;
                    _uow.Context.Entry(ledgerEntry).State = EntityState.Detached;
                }
            }
        }

        _logger.LogInformation("Leave accrual {Date}: processed {Count} credit record(s)", processDate, count);
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)    == true;
}

public class LeaveAccrualWorkerDefinition : ConsumerDefinition<LeaveAccrualWorker>
{
    public LeaveAccrualWorkerDefinition()
    {
        EndpointName           = "hrms-leave-accrual-que";
        ConcurrentMessageLimit = 1;
    }
}
