using Hrms.Core.Services;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.LeaveWorkers;

/// <summary>
/// Opens this fiscal year's (empty) LeaveCredits record for every eligible active employee on an
/// accrual-based leave type, so LeaveAccrualWorker has something to accrue onto. Nothing else
/// ever created these records -- the period grant only handles lump-sum (AccrualBasis.None)
/// types -- so an accrual-based type used to accrue for nobody unless HR created each record by
/// hand. Eligibility and the active-status whitelist mirror LeavePeriodGrantWorker.
///
/// Runs on the 1st of each month, before the accrual (see DailyBenefitsWorker), which also picks
/// up employees who became eligible since last month. Annually-accrued types are only opened on
/// the fiscal year's first day -- the only day they accrue -- so no empty records sit around.
/// Idempotent: existing records are left alone; a duplicate-key race is ignored.
/// </summary>
public class AccrualCreditOpener
{
    private readonly IUnitOfWorkService _uow;
    private readonly DailyRecordService _dailyRecordService;
    private readonly ILogger<AccrualCreditOpener> _logger;

    public AccrualCreditOpener(IUnitOfWorkService uow, DailyRecordService dailyRecordService, ILogger<AccrualCreditOpener> logger)
    {
        _uow = uow;
        _dailyRecordService = dailyRecordService;
        _logger = logger;
    }

    public async Task<int> OpenMissingAsync(DateOnly processDate, int fiscalStartMonth, CancellationToken token)
    {
        var isFiscalYearStart = FiscalYearHelper.IsFiscalYearStart(processDate, fiscalStartMonth);
        var leaves = (await _uow.Repository
                .Find<Leave>(x =>
                    x.AccrualBasis == AccrualBasis.Monthly ||
                    x.AccrualBasis == AccrualBasis.PerPayPeriod ||
                    x.AccrualBasis == AccrualBasis.Annually)
                .AsNoTracking()
                .ToListAsync(token))
            .Where(x => x.AccrualBasis != AccrualBasis.Annually || isFiscalYearStart)
            .ToList();
        if (leaves.Count == 0) return 0;

        var fiscalYear = FiscalYearHelper.FiscalYearOf(processDate, fiscalStartMonth);
        var periodStart = FiscalYearHelper.PeriodStart(fiscalYear, fiscalStartMonth);
        var periodEnd = FiscalYearHelper.PeriodEnd(fiscalYear, fiscalStartMonth);

        var employees = await _uow.Repository
            .Find<Employee>(x =>
                x.EmploymentStatus == EmploymentStatus.Regular ||
                x.EmploymentStatus == EmploymentStatus.Probationary ||
                x.EmploymentStatus == EmploymentStatus.Contract)
            .AsNoTracking()
            .Select(x => new { x.Id, x.HireDate })
            .ToListAsync(token);
        if (employees.Count == 0) return 0;

        var leaveIds = leaves.Select(x => x.Id).ToList();
        var existing = (await _uow.Repository
                .Find<LeaveCredits>(x => x.PeriodYear == fiscalYear && leaveIds.Contains(x.LeaveId))
                .AsNoTracking()
                .Select(x => new { x.EmployeeId, x.LeaveId })
                .ToListAsync(token))
            .Select(x => (x.EmployeeId, x.LeaveId))
            .ToHashSet();

        var presentDays = leaves.Any(l => l.EligibilityBasis == LeaveEligibilityBasis.PresentDays)
            ? await _dailyRecordService.CountPresentDaysBatchAsync(employees.Select(e => e.Id), processDate, token)
            : new Dictionary<Guid, int>();

        var opened = new List<LeaveCredits>();
        foreach (var leave in leaves)
        {
            foreach (var employee in employees)
            {
                if (existing.Contains((employee.Id, leave.Id))) continue;
                var monthsServed = LeaveEligibilityCalculator.MonthsBetween(employee.HireDate, processDate);
                if (!LeaveEligibilityCalculator.IsServiceRequirementMet(leave, monthsServed, presentDays.GetValueOrDefault(employee.Id)))
                    continue;

                opened.Add(new LeaveCredits
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employee.Id,
                    LeaveId = leave.Id,
                    PeriodYear = fiscalYear,
                    FromDate = periodStart,
                    ToDate = periodEnd,
                    Granted = 0m,
                    Used = 0m,
                    Balance = 0m,
                });
            }
        }
        if (opened.Count == 0) return 0;

        _uow.Repository.AddRange(opened);
        try
        {
            await _uow.CommitChangesAsync("", token);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("uplicate", StringComparison.OrdinalIgnoreCase) == true ||
                                           ex.InnerException?.Message.Contains("unique", StringComparison.OrdinalIgnoreCase) == true)
        {
            _logger.LogWarning("Accrual credit opening {Date}: records already opened by a concurrent run", processDate);
            return 0;
        }

        _logger.LogInformation("Accrual credit opening {Date}: opened {Count} record(s) for FY{Year}", processDate, opened.Count, fiscalYear);
        return opened.Count;
    }
}
