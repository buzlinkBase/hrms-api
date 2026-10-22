using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.UniformAllowance;

/// <summary>
/// A client's uniform allowance policy versions (Client Settings > Uniform Allowance). A change is
/// always a new version starting on a future day; versions the credit run has already used are
/// read-only, so every past credit keeps the rules it was decided under. Who changed what is
/// recorded on the version (creator and reason) and in the audit trail ([Audited]).
/// </summary>
public class UniformAllowancePolicyService : BaseService<UniformAllowancePolicy>
{
    private static readonly HashSet<EmploymentStatus> SeparatedStatuses =
    [
        EmploymentStatus.Terminated, EmploymentStatus.Resigned, EmploymentStatus.Retired, EmploymentStatus.Deceased,
    ];

    public UniformAllowancePolicyService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<UniformAllowancePolicyListModel> ListAsync(Guid clientId, CancellationToken token)
    {
        var client = await RequireClientAsync(clientId, token);
        var versions = await VersionsAsync(clientId, token);
        var last = await LastProcessedCreditDateAsync(clientId, token);
        var current = UniformAllowancePolicyResolver.Resolve(versions, client, PhTime.Today(DateTime.UtcNow));
        var legacy = UniformPolicySnapshot.FromLegacy(client);

        return new UniformAllowancePolicyListModel
        {
            Versions = versions.OrderByDescending(v => v.EffectiveFrom).Select(v => ToModel(v, last)).ToList(),
            Legacy = legacy == null ? null : UniformPolicyModels.FromSnapshot(legacy, clientId),
            CurrentPolicyId = current?.PolicyId,
            LastProcessedCreditDate = last,
        };
    }

    public async Task<UniformAllowancePolicyModel> CreateAsync(
        Guid clientId, UpsertUniformAllowancePolicy payload, Guid? userId, string userName, CancellationToken token)
    {
        await RequireClientAsync(clientId, token);
        ThrowIfInvalid(payload);
        await EnsureStartsAfterLastCreditAsync(clientId, payload.EffectiveFrom, token);
        await EnsureDateFreeAsync(clientId, payload.EffectiveFrom, exceptId: null, token);

        var entity = new UniformAllowancePolicy
        {
            ClientId = clientId,
            CreatedByUserId = userId,
            CreatedByName = userName,
        };
        Apply(payload, entity);
        Context.UniformAllowancePolicies.Add(entity);
        await CommitChangesAsync(token);
        return ToModel(entity, null);
    }

    /// <summary>Changes a version that hasn't taken effect yet.</summary>
    public async Task<UniformAllowancePolicyModel> UpdateAsync(
        Guid clientId, Guid policyId, UpsertUniformAllowancePolicy payload, CancellationToken token)
    {
        var entity = await RequireVersionAsync(clientId, policyId, token);
        ThrowIfInvalid(payload);
        await EnsureStartsAfterLastCreditAsync(clientId, entity.EffectiveFrom, token);
        await EnsureStartsAfterLastCreditAsync(clientId, payload.EffectiveFrom, token);
        await EnsureDateFreeAsync(clientId, payload.EffectiveFrom, policyId, token);

        Apply(payload, entity);
        await CommitChangesAsync(token);
        return ToModel(entity, null);
    }

    /// <summary>Removes a version that hasn't taken effect yet.</summary>
    public async Task DeleteAsync(Guid clientId, Guid policyId, CancellationToken token)
    {
        var entity = await RequireVersionAsync(clientId, policyId, token);
        await EnsureStartsAfterLastCreditAsync(clientId, entity.EffectiveFrom, token);
        Context.UniformAllowancePolicies.Remove(entity);
        await CommitChangesAsync(token);
    }

    /// <summary>The client's versions, oldest first.</summary>
    public Task<List<UniformAllowancePolicy>> VersionsAsync(Guid clientId, CancellationToken token) =>
        Context.UniformAllowancePolicies.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .OrderBy(x => x.EffectiveFrom)
            .ToListAsync(token);

    /// <summary>
    /// The last day a credit was decided for the client -- from the credit log, or from accruals
    /// posted before the credit log existed.
    /// </summary>
    public async Task<DateOnly?> LastProcessedCreditDateAsync(Guid clientId, CancellationToken token)
    {
        var logged = await Context.UniformAllowanceCreditLogs.AsNoTracking()
            .Where(x => x.ClientId == clientId)
            .MaxAsync(x => (DateOnly?)x.CreditDate, token);
        var accrued = await Context.UniformAllowanceLedgers.AsNoTracking()
            .Where(x => x.EntryType == UniformAllowanceEntryType.Accrual && x.Employee.ClientId == clientId)
            .MaxAsync(x => (DateOnly?)x.EntryDate, token);
        return logged > accrued || accrued == null ? logged : accrued;
    }

    /// <summary>Every rule a version must satisfy, as messages for the form. Empty when it's valid.</summary>
    public static List<string> Validate(UpsertUniformAllowancePolicy p)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(p.ChangeReason)) errors.Add("Say why the rules are changing.");
        if (!p.IsEnabled) return errors;

        if (p.Amount <= 0) errors.Add("Enter an amount above zero.");
        if (p.Frequency != UniformCreditFrequency.HireAnniversary && p.CreditDay is < 1 or > 28)
            errors.Add("Credit day must be between 1 and 28, so every month has it.");
        if (p.Frequency is UniformCreditFrequency.Quarterly or UniformCreditFrequency.SemiAnnual or UniformCreditFrequency.Annual &&
            p.StartMonth is < 1 or > 12)
            errors.Add("Pick the month the cycle starts in.");
        if (p.AmountMode == UniformAmountMode.ProratedByPresentDays && p.ExpectedDaysPerPeriod is not > 0)
            errors.Add("Prorating needs the present days that earn the full amount.");
        if (p.EligibleStatuses.Count == 0) errors.Add("Pick at least one employment status that qualifies.");
        if (p.EligibleStatuses.Any(SeparatedStatuses.Contains)) errors.Add("Separated employees can't qualify.");
        if (p.WaitingMonths is < 0 or > 120) errors.Add("Waiting period must be 0 to 120 months.");
        if (p.MinPresentDays is < 0) errors.Add("Required present days can't be negative.");
        if (p.MaxBalance is <= 0) errors.Add("A balance cap must be above zero (or leave it empty for no cap).");
        if (p.Expiry == UniformBalanceExpiry.ResetAnnually && p.ExpiryMonth is not (>= 1 and <= 12))
            errors.Add("Pick the month the balance resets.");
        if (p.Expiry == UniformBalanceExpiry.ExpireAfterMonths && p.ExpiryAfterMonths is not (>= 1 and <= 120))
            errors.Add("Credits must expire after 1 to 120 months.");
        if (p.RehireRule == UniformRehireRule.BridgeWithinDays && p.RehireBridgeDays is not (>= 1 and <= 3650))
            errors.Add("Enter how many days after separating a rehire still keeps their service (1 to 3,650).");
        return errors;
    }

    private static void ThrowIfInvalid(UpsertUniformAllowancePolicy payload)
    {
        var errors = Validate(payload);
        if (errors.Count > 0) throw new ValidationException(string.Join(" ", errors));
    }

    // Past days already ran (a credit due on one would be missed silently), and any day up to the
    // last decided credit is settled history.
    private async Task EnsureStartsAfterLastCreditAsync(Guid clientId, DateOnly effectiveFrom, CancellationToken token)
    {
        var today = PhTime.Today(DateTime.UtcNow);
        if (effectiveFrom < today)
            throw new ValidationException("A change can start today at the earliest — past credits keep the rules they were decided under.");
        var last = await LastProcessedCreditDateAsync(clientId, token);
        if (last is { } settled && effectiveFrom <= settled)
            throw new ValidationException(
                $"Credits were already decided on {settled:MMM d, yyyy} under the current rules — start the change after that.");
    }

    private async Task EnsureDateFreeAsync(Guid clientId, DateOnly effectiveFrom, Guid? exceptId, CancellationToken token)
    {
        var taken = await Context.UniformAllowancePolicies.AnyAsync(
            x => x.ClientId == clientId && x.EffectiveFrom == effectiveFrom && x.Id != exceptId, token);
        if (taken) throw new ValidationException("Another version already starts on that date — edit that one instead.");
    }

    private async Task<Client> RequireClientAsync(Guid clientId, CancellationToken token) =>
        await Context.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, token)
        ?? throw new NotFoundException("Client not found.");

    private async Task<UniformAllowancePolicy> RequireVersionAsync(Guid clientId, Guid policyId, CancellationToken token) =>
        await Context.UniformAllowancePolicies.FirstOrDefaultAsync(x => x.Id == policyId && x.ClientId == clientId, token)
        ?? throw new NotFoundException("Uniform allowance policy not found.");

    private static void Apply(UpsertUniformAllowancePolicy p, UniformAllowancePolicy e)
    {
        e.EffectiveFrom = p.EffectiveFrom;
        e.IsEnabled = p.IsEnabled;
        e.Frequency = p.Frequency;
        e.CreditDay = p.CreditDay;
        e.StartMonth = p.StartMonth;
        e.AmountMode = p.AmountMode;
        e.Amount = p.Amount;
        e.ExpectedDaysPerPeriod = p.AmountMode == UniformAmountMode.ProratedByPresentDays ? p.ExpectedDaysPerPeriod : null;
        e.EligibleStatuses = p.EligibleStatuses.Distinct().ToList();
        e.WaitingMonths = p.WaitingMonths;
        e.MinPresentDays = p.MinPresentDays;
        e.MaxBalance = p.MaxBalance;
        e.Expiry = p.Expiry;
        e.ExpiryMonth = p.Expiry == UniformBalanceExpiry.ResetAnnually ? p.ExpiryMonth : null;
        e.ExpiryAfterMonths = p.Expiry == UniformBalanceExpiry.ExpireAfterMonths ? p.ExpiryAfterMonths : null;
        e.OnSeparation = p.OnSeparation;
        e.RehireRule = p.RehireRule;
        e.RehireBridgeDays = p.RehireRule == UniformRehireRule.BridgeWithinDays ? p.RehireBridgeDays : null;
        e.ChangeReason = p.ChangeReason.Trim();
    }

    private static UniformAllowancePolicyModel ToModel(UniformAllowancePolicy v, DateOnly? lastProcessed)
    {
        var model = UniformPolicyModels.FromSnapshot(UniformPolicySnapshot.From(v), v.ClientId);
        model.ChangeReason = v.ChangeReason;
        model.CreatedAt = v.CreatedAt == default ? null : PhTime.AsUtc(v.CreatedAt);
        model.CreatedByName = v.CreatedByName;
        model.IsLocked = lastProcessed is { } settled && v.EffectiveFrom <= settled;
        return model;
    }
}

public static class UniformPolicyModels
{
    /// <summary>The rules as the API returns them; a legacy snapshot comes back with IsLegacy and no Id.</summary>
    public static UniformAllowancePolicyModel FromSnapshot(UniformPolicySnapshot s, Guid clientId) => new()
    {
        Id = s.PolicyId,
        ClientId = clientId,
        IsLegacy = s.IsLegacy,
        IsLocked = s.IsLegacy,
        EffectiveFrom = s.EffectiveFrom,
        IsEnabled = s.IsEnabled,
        Frequency = s.Frequency,
        CreditDay = s.CreditDay,
        StartMonth = s.StartMonth,
        AmountMode = s.AmountMode,
        Amount = s.Amount,
        ExpectedDaysPerPeriod = s.ExpectedDaysPerPeriod,
        EligibleStatuses = s.EligibleStatuses.ToList(),
        WaitingMonths = s.WaitingMonths,
        MinPresentDays = s.MinPresentDays,
        MaxBalance = s.MaxBalance,
        Expiry = s.Expiry,
        ExpiryMonth = s.ExpiryMonth,
        ExpiryAfterMonths = s.ExpiryAfterMonths,
        OnSeparation = s.OnSeparation,
        RehireRule = s.RehireRule,
        RehireBridgeDays = s.RehireBridgeDays,
        ChangeReason = s.IsLegacy ? "Settings from before uniform allowance policies" : string.Empty,
    };
}
