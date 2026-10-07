namespace DTR.Core;

// Resolves the OT Inclusion policy in effect for the current employee: the client's override
// when the employee belongs to a client that has one, otherwise the company setting.
public static class OvertimeInclusionPolicyResolver
{
    public static OvertimeInclusionPolicy Resolve(TimeContext context)
    {
        var data = context.Payload.Data;
        if (data.Employee.ClientId.HasValue)
        {
            var clientPolicy = context.Payload.Provider.ClientPolicyProvider.GetPolicy(data.Employee.ClientId.Value);
            if (clientPolicy?.OvertimeInclusionPolicy is { } clientInclusion) return clientInclusion;
        }
        return data.CompanyPolicy.OTInclusionPolicy;
    }
}
