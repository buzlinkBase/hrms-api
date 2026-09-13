namespace Hrms.Core.Policies.DeductionPolicies;

// Applies an optional per-client maximum monthly EE (employee) statutory deduction cap —
// Setup > Client > Settings > Statutory Capping (Max SSS/PhilHealth/Pag-IBIG). A cap only
// lowers the monthly EE target that SSSHelper/PHICHelper/HDMFHelper.GetBalance allocate across
// this month's cutoffs; it never affects the ER (employer) share, since "capping" here means
// capping what's deducted from the employee, not the employer's contribution obligation.
internal static class StatutoryCapHelper
{
    public static decimal ApplyClientCap(DeductionPayloadContext context, StatutoryCapType type, decimal computedMonthlyEE)
    {
        if (context.Employee.ClientId is not { } clientId) return computedMonthlyEE;
        if (!context.Payload.ClientStatutoryCaps.TryGetValue(new ClientStatutoryCapKey(clientId, type), out var cap))
            return computedMonthlyEE;
        if (cap <= 0) return computedMonthlyEE;
        return Math.Min(computedMonthlyEE, cap);
    }
}
