using Hrms.Domain.Entities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

// Fires when a leave application is filed. For PerEvent leave types (Maternity, Paternity, etc.)
// it creates the LeaveCredits record immediately so the employee can see their allocation
// before the application is approved. DeductCreditsAsync then runs normally on approval.
//
// Idempotency: pre-check prevents redundant work on redelivery; the unique index on
// (EmployeeId, LeaveId, PeriodYear) is the hard stop if two messages race past the pre-check.
public class LeaveGrantOnEventWorker : IConsumer<LeaveApplicationCreated>
{
    private readonly IUnitOfWorkService _uow;
    private readonly ILogger<LeaveGrantOnEventWorker> _logger;

    public LeaveGrantOnEventWorker(IUnitOfWorkService uow, ILogger<LeaveGrantOnEventWorker> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LeaveApplicationCreated> context)
    {
        var msg   = context.Message;
        var token = context.CancellationToken;

        var leave = await _uow.Repository
            .Find<Leave>(x => x.Id == msg.LeaveId)
            .AsNoTracking()
            .FirstOrDefaultAsync(token);

        if (leave == null || leave.AccrualBasis != AccrualBasis.PerEvent) return;
        if (leave.PaySource == PaySource.Government) return;

        var today = DateTime.UtcNow;
        var year  = today.Year;

        // Pre-check — fast exit on redelivery
        var exists = await _uow.Repository
            .Find<LeaveCredits>(x =>
                x.EmployeeId == msg.EmployeeId &&
                x.LeaveId    == msg.LeaveId    &&
                x.PeriodYear == year)
            .AnyAsync(token);

        if (exists) return;

        var credits = new LeaveCredits
        {
            Id         = Guid.NewGuid(),
            EmployeeId = msg.EmployeeId,
            LeaveId    = msg.LeaveId,
            PeriodYear = year,
            FromDate   = today,
            ToDate     = today.AddYears(1),
            Granted    = (decimal)leave.Credits,
            Used       = 0m,
            Balance    = (decimal)leave.Credits,
        };

        _uow.Repository.Add(credits);
        _uow.Repository.Add(new LeaveLedger
        {
            Id                     = Guid.NewGuid(),
            EmployeeId             = msg.EmployeeId,
            LeaveId                = msg.LeaveId,
            LeaveCreditsId         = credits.Id,
            EntryType              = LedgerEntryType.Grant,
            EntryDate              = DateOnly.FromDateTime(today),
            Add                    = credits.Granted,
            Less                   = 0m,
            Balance                = credits.Balance,
            Particulars            = $"Per-event grant — {leave.Description} ({leave.Credits} day(s))",
            ReferenceApplicationId = msg.ApplicationId,
        });

        try
        {
            await _uow.CommitChangesAsync("",token);
            _logger.LogInformation(
                "Granted {Days} day(s) of {LeaveCode} to employee {EmployeeId}",
                leave.Credits, leave.Code, msg.EmployeeId);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Another message won the race — credit already exists, safe to ignore
            _logger.LogWarning(
                "Duplicate grant skipped for employee {EmployeeId} / leave {LeaveCode} ({Year})",
                msg.EmployeeId, leave.Code, year);
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)    == true;
}

public class LeaveGrantOnEventWorkerDefinition : ConsumerDefinition<LeaveGrantOnEventWorker>
{
    public LeaveGrantOnEventWorkerDefinition()
    {
        EndpointName = "hrms-leave-grant-event-que";
        ConcurrentMessageLimit = 1;
    }
}
