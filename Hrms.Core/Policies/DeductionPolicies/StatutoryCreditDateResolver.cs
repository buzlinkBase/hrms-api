namespace Hrms.Core.Policies.DeductionPolicies;

/// <summary>
/// Resolves which calendar month a payroll cutoff's SSS/PhilHealth/Pag-IBIG withholding is
/// credited against, per CrossMonthStatutoryCreditPolicy. For a same-month cutoff FromDate
/// and ToDate share a month, so the policy has no effect; it only matters when the cutoff
/// crosses a month boundary.
/// </summary>
public static class StatutoryCreditDateResolver
{
    // payDate is optional: under the PayDate policy a run without one credits to toDate.
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
