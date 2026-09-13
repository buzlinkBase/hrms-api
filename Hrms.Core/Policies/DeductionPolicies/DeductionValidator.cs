namespace Hrms.Core.Policies;

// Setup > Company Policy > Minimum Take-Home Pay — a deduction (statutory or scheduled) may
// never cut an employee's remaining gross balance below RequiredTakehomePercentage of their
// gross income for the period. RemainingGrossBalance is the running balance as deductions are
// applied through the pipeline (seeded from PayrollLine.GrossIncome in DeductionPipeline) —
// true NetPay isn't computed until after the whole pipeline finishes, so the floor has to be
// derived from GrossIncome, not NetPay.
public class DeductionValidator
{
    public static bool CanApply(decimal forDeduction, DeductionPipeData line, DeductionPayloadContext context)
    {
        var minimumTakeHome = context.PayrollLine.GrossIncome * (context.Payload.CompanyPolicy.RequiredTakehomePercentage / 100m);
        var remainingAfterDeduction = line.RemainingGrossBalance - forDeduction;
        return remainingAfterDeduction >= minimumTakeHome;
    }
}
