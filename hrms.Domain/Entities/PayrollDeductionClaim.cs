using BuzlinkRepository;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Domain.Entities;

// Which payroll run took an employee's deductions (statutory, tax, loans, general other income)
// for one deduction period -- a cutoff of the employee's own payroll group. A reliever paid by
// several client runs in the same cutoff is deducted in exactly one of them; the unique index on
// (EmployeeId, PeriodStart) is what guarantees it even when two runs are generated at once.
// Written when a run is generated, released when the run is deleted. Hard-deleted so a released
// claim frees the unique slot immediately.
[DisableSoftDelete]
public class PayrollDeductionClaim : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public virtual Employee Employee { get; set; } = null!;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public Guid PayrollBatchId { get; set; }
}
