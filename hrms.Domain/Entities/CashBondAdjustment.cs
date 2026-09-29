using Hrms.Domain.Entities.EmployeeEntities;
using Onepunch.AuditEngine;

namespace Hrms.Domain.Entities;

// Payroll Reports > Cash Bond Tracking -- a manual HR entry against an employee's cash bond
// collected total, e.g. the opening balance of cash bond deducted before this system was adopted
// (prior payrolls that exist only in the old system), or a correction. Cash bond collected is
// otherwise derived from payroll runs (Payroll.CashBondDeduction, see CashBondDeductionPolicy);
// these entries are added on top. Append-only, same signed-Amount shape as the other ledgers.
[Audited]
public class CashBondAdjustment : BaseEntity, IEmployeeOwned, IEntryRecord
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    // Counts toward the total for any "as of" date on or after this.
    public DateOnly EntryDate { get; set; }
    // Signed pesos: + adds to the collected total (e.g. an opening balance), − corrects it down.
    public decimal Amount { get; set; }
    public string Particulars { get; set; } = string.Empty;
}
