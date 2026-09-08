using BuzlinkRepository;

namespace Hrms.Domain.Entities;

// Locks a calendar year's payroll data once its Year-End Tax Adjustment has been posted —
// protects the audit trail the annualization was computed from. One row per (tenant, Year).
// See YearLockService, and the guard clauses at the top of PayrollProcessorService.GenerateAsync/
// ThirteenthMonthPayrollService.GenerateAsync/LastPayrollService.GenerateAsync/
// TaxAnnualizationService.GenerateAsync and inside PayrollBatchLifecycleService.
[DisableSoftDelete]
public class YearLock : BaseEntity
{
    public int Year { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? ReopenedAt { get; set; }
}
