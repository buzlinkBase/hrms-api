
namespace Hrms.Core.Policies;

// Cash Bond is a flat, recurring deduction sourced straight from Employee.CashBond -- unlike
// ScheduledDeductionPolicy (the generic loan/DeductionApplication system this replaces for Cash
// Bond specifically), there's no amortization schedule or target to track: the same amount is
// deducted every regular payroll run. Still respects the Minimum Take-Home Pay floor like every
// other deduction (see DeductionValidator.CanApply).
internal class CashBondDeductionPolicy : PayrollPolicyBase<DeductionPipeData, DeductionPayloadContext>
{
    public override DeductionPipeData ApplyIfSatisfied(DeductionPipeData line, DeductionPayloadContext context)
    {
        var amount = context.Employee.CashBond;
        if (line.IsLimit || amount <= 0) return line;

        if (!DeductionValidator.CanApply(amount, line, context))
        {
            line.IsLimit = true;
            return line;
        }

        line.RemainingGrossBalance -= amount;
        line.RunningTotal += amount;
        line.CashBond = amount;
        return line;
    }
}
