using Hrms.Domain.Entities.EmployeeEntities;
using Onepunch.AuditEngine;

namespace Hrms.Domain.Entities;

// Payroll Reports > Cash Bond Tracking > rate history -- one row each time HR changes an
// employee's per-run cash bond (Employee.CashBond), whether from the report or from Employee
// Details. A history only: it never moves the collected total, which stays what payroll actually
// deducted plus manual CashBondAdjustment entries, less Last Pay refunds. The new rate applies from
// the next payroll run (CashBondDeductionPolicy reads Employee.CashBond when a run is generated);
// runs already generated keep the amount they deducted. Append-only. Who changed it comes from the
// audit trail ([Audited]).
[Audited]
public class CashBondRateChange : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public decimal OldRate { get; set; }
    public decimal NewRate { get; set; }
    public string Reason { get; set; } = string.Empty;

    // The one rule for "did the rate change": null when it did not, so both places that edit the
    // rate (the report's quick edit and Employee Details) log a change the same way.
    public static CashBondRateChange? Between(Guid employeeId, decimal oldRate, decimal newRate, string? reason) =>
        oldRate == newRate
            ? null
            : new CashBondRateChange
            {
                EmployeeId = employeeId,
                OldRate = oldRate,
                NewRate = newRate,
                Reason = reason?.Trim() ?? string.Empty,
            };
}
