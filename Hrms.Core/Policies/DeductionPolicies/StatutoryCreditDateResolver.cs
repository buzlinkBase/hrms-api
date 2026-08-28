namespace Hrms.Core.Policies.DeductionPolicies;

/// <summary>
/// Resolves which calendar month a payroll cutoff's SSS/PhilHealth/Pag-IBIG withholding is
/// credited against, per CrossMonthStatutoryCreditPolicy. For a same-month cutoff FromDate
/// and ToDate share a month, so the policy has no effect; it only matters when the cutoff
/// crosses a month boundary.
/// </summary>
public static class StatutoryCreditDateResolver
{
    // payDate is required by the caller when policy is PayDate (see
    // PayrollProcessorService.CalculateAsync's up-front guard) — the fallback to toDate
    // here is defense in depth only, not the primary enforcement.
    public static DateOnly Resolve(DateOnly fromDate, DateOnly toDate, CrossMonthStatutoryCreditPolicy policy, DateOnly? payDate = null)
    {
        return policy switch
        {
            CrossMonthStatutoryCreditPolicy.CutoffEndMonth => toDate,
            CrossMonthStatutoryCreditPolicy.PayDate => payDate ?? toDate,
            _ => fromDate,
        };
    }
}
