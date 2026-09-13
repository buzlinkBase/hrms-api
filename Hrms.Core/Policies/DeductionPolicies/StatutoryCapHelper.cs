namespace Hrms.Core.Policies.DeductionPolicies;

// Setup > Client > Settings > Statutory Capping (Max SSS/PhilHealth/Pag-IBIG) — an optional
// per-client maximum on the EMPLOYEE (EE) share of SSS/PhilHealth/Pag-IBIG. This only looks up
// the configured cap; SSSHelper/PHICHelper/HDMFHelper.GetTable use it to downgrade to a lower,
// fully self-consistent bracket (both EE and ER read from the SAME real government-table row)
// whenever the employee's natural bracket would exceed it — capping is never a standalone
// reduction of EE alone, so the result always reads as an ordinary bracket match (as if the
// employee's gross simply put them in a lower bracket), not a partial override with a
// mismatched ER left over from a higher bracket.
internal static class StatutoryCapHelper
{
    // Returns null when there's no employee ClientId, no cap configured for that client/type,
    // or the configured cap is <= 0 (0/unset both mean "uncapped" — see GeneralSettingsUtil.
    // ParsePositiveDecimalOrNull, which never lets a non-positive value reach this dictionary).
    public static decimal? GetClientCap(DeductionPayloadContext context, StatutoryCapType type)
    {
        if (context.Employee.ClientId is not { } clientId) return null;
        if (!context.Payload.ClientStatutoryCaps.TryGetValue(new ClientStatutoryCapKey(clientId, type), out var cap))
            return null;
        return cap > 0 ? cap : null;
    }
}
