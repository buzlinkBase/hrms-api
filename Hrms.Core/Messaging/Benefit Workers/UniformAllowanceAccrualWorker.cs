using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.BenefitWorkers;

// Runs on the 1st of each month (published by LeaveSchedulerService.PublishForTenant alongside
// RunLeaveAccrual -- reuses that same daily-tick/tenant-loop infrastructure rather than a second
// scheduler). Credits Client.UniformAllowance's flat monthly rate onto each eligible employee's
// UniformAllowanceFund.Balance.
//
// Idempotency: before accruing each employee, checks whether an Accrual ledger entry already
// exists for the same year+month (fast pre-check). UniformAllowanceLedger.AccrualDedupeKey (a
// unique index, populated only for Accrual entries) is the hard stop if two instances/
// redeliveries race past that check -- see UniformAllowanceFundService.AccrueAsync, which
// commits per employee (not batched) specifically so one collision can't roll back another
// employee's legitimate accrual in the same run. Mirrors LeaveAccrualWorker exactly.
//
// Eligibility: only employees whose EmploymentStatus is Regular/Probationary/Contract accrue
// (mirrors LeavePeriodGrantWorker's active-employee whitelist) -- unlike Retirement's per-cutoff
// accrual, which naturally stops once a separated employee has no more payroll runs, this
// calendar-driven accrual has nothing else to stop it.
public class UniformAllowanceAccrualWorker : IConsumer<RunUniformAllowanceAccrual>
{
    // Client.UniformAllowanceBasis.PresentDays -- an employee needs at least this many present
    // days within the calendar month for that month's flat rate to accrue (pass/fail, no partial
    // amounts -- mirrors how Leave's own Present-Days eligibility is pass/fail, not prorated).
    private const int PresentDaysThreshold = 20;

    private readonly IUnitOfWorkService _uow;
    private readonly UniformAllowanceFundService _fundService;
    private readonly DailyRecordService _dailyRecordService;
    private readonly ILogger<UniformAllowanceAccrualWorker> _logger;

    public UniformAllowanceAccrualWorker(
        IUnitOfWorkService uow,
        UniformAllowanceFundService fundService,
        DailyRecordService dailyRecordService,
        ILogger<UniformAllowanceAccrualWorker> logger)
    {
        _uow = uow;
        _fundService = fundService;
        _dailyRecordService = dailyRecordService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RunUniformAllowanceAccrual> context)
    {
        var processDate = context.Message.ProcessDate;
        var token = context.CancellationToken;
        var count = 0;

        var clients = await _uow.Repository
            .Find<Client>(x => x.UniformAllowance != null && x.UniformAllowance > 0)
            .AsNoTracking()
            .ToListAsync(token);
        if (clients.Count == 0) return;

        var clientMap = clients.ToDictionary(x => x.Id);
        var clientIds = clientMap.Keys.ToHashSet();

        var employees = await _uow.Repository
            .Find<Employee>(x =>
                x.ClientId.HasValue && clientIds.Contains(x.ClientId.Value) &&
                (x.EmploymentStatus == EmploymentStatus.Regular ||
                 x.EmploymentStatus == EmploymentStatus.Probationary ||
                 x.EmploymentStatus == EmploymentStatus.Contract))
            .AsNoTracking()
            .ToListAsync(token);
        if (employees.Count == 0) return;

        var employeeIds = employees.Select(x => x.Id).ToList();

        // Already-accrued-this-month guard, one batched query (mirrors LeaveAccrualWorker).
        var alreadyAccruedIds = await _uow.Repository
            .Find<UniformAllowanceLedger>(x =>
                employeeIds.Contains(x.EmployeeId) &&
                x.EntryType == UniformAllowanceEntryType.Accrual &&
                x.EntryDate.Year == processDate.Year &&
                x.EntryDate.Month == processDate.Month)
            .Select(x => x.EmployeeId)
            .ToHashSetAsync(token);

        var monthStart = new DateOnly(processDate.Year, processDate.Month, 1);
        var presentDaysBasisEmployeeIds = employees
            .Where(e => !alreadyAccruedIds.Contains(e.Id) &&
                        clientMap[e.ClientId!.Value].UniformAllowanceBasis == BenefitAccrualBasis.PresentDays)
            .Select(e => e.Id)
            .ToList();
        var presentDaysCounts = await _dailyRecordService
            .CountPresentDaysInRangeBatchAsync(presentDaysBasisEmployeeIds, monthStart, processDate, token);

        foreach (var employee in employees)
        {
            if (alreadyAccruedIds.Contains(employee.Id)) continue;

            var client = clientMap[employee.ClientId!.Value];
            var rate = client.UniformAllowance!.Value;

            var monthsServed = LeaveEligibilityCalculator.MonthsBetween(employee.HireDate, processDate);
            var presentDaysThisMonth = presentDaysCounts.GetValueOrDefault(employee.Id);
            if (!IsMonthEarned(client.UniformAllowanceBasis, monthsServed, presentDaysThisMonth)) continue;

            var committed = await _fundService.AccrueAsync(
                employee.Id, rate, processDate, $"Uniform allowance accrual — {processDate:MMM yyyy}", token);
            if (committed)
            {
                count++;
            }
            else
            {
                _logger.LogWarning(
                    "Duplicate accrual skipped for employee {EmployeeId} ({Date}) — already committed by another instance/redelivery",
                    employee.Id, processDate);
            }
        }

        _logger.LogInformation("Uniform allowance accrual {Date}: processed {Count} employee(s)", processDate, count);
    }

    // Whether this calendar month's flat rate is earned for one employee. TenureMonths: at
    // least one full month since hire (a mid-month hire's accrual starts the following month).
    // PresentDays: a pass/fail attendance gate -- present >= PresentDaysThreshold that month, no
    // partial amounts, mirroring how Leave's own Present-Days eligibility is pass/fail too.
    // `internal` (not private) so this decision can be unit tested without a database.
    internal static bool IsMonthEarned(BenefitAccrualBasis basis, int monthsServed, int presentDaysThisMonth) =>
        basis == BenefitAccrualBasis.PresentDays
            ? presentDaysThisMonth >= PresentDaysThreshold
            : monthsServed >= 1;
}

public class UniformAllowanceAccrualWorkerDefinition : ConsumerDefinition<UniformAllowanceAccrualWorker>
{
    public UniformAllowanceAccrualWorkerDefinition()
    {
        EndpointName = "hrms-uniform-allowance-accrual-que";
        ConcurrentMessageLimit = 1;
    }
}
