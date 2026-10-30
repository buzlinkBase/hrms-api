using Hrms.Domain.Entities;

namespace Hrms.Core.Services.Compliance;

// Who is saving, for the acknowledgment record.
public sealed record ComplianceActor(Guid? UserId, string? Email, string? IpAddress);

public sealed class ComplianceAcknowledgmentModel
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string SettingKey { get; set; } = string.Empty;
    public string SettingLabel { get; set; } = string.Empty;
    public string StatutoryValue { get; set; } = string.Empty;
    public string ConfiguredValue { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? IpAddress { get; set; }
    public DateTime AcknowledgedAtUtc { get; set; }
}

public sealed record ComplianceAcknowledgmentPage(List<ComplianceAcknowledgmentModel> Items, int Total, int Page, int PageSize);

// Settings that differ from the statutory (DOLE) defaults must be acknowledged once per value: the
// first save that brings one in is refused (DeviationAcknowledgmentRequiredException) until the
// caller confirms, and the confirmation is recorded. Saving the same value again asks nothing.
// Only while the Terms of Service are live (ITermsLiveness); before that nothing is asked or recorded.
// Doesn't commit: the caller's save does, so the setting and its acknowledgment land together.
public class ComplianceAcknowledgmentService : BaseService<ComplianceAcknowledgment>
{
    public const string CompanyScope = "company";
    public static string ClientScope(Guid clientId) => $"client:{clientId}";

    private readonly ITermsLiveness _terms;
    private readonly TimeProvider _clock;

    public ComplianceAcknowledgmentService(IUnitOfWorkService uow, ITermsLiveness terms, TimeProvider? clock = null) : base(uow)
    {
        _terms = terms;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task EnsureAcknowledgedAsync(
        string scope,
        IReadOnlyList<StatutoryDeviation> deviations,
        bool acknowledged,
        ComplianceActor actor,
        CancellationToken token = default)
    {
        if (deviations.Count == 0 || !await _terms.IsLiveAsync(token)) return;

        var keys = deviations.Select(d => d.Key).ToList();
        var done = await Context.ComplianceAcknowledgments.AsNoTracking()
            .Where(a => a.Scope == scope && keys.Contains(a.SettingKey))
            .Select(a => new { a.SettingKey, a.ConfiguredValue })
            .ToListAsync(token);
        var pending = deviations
            .Where(d => !done.Any(a => a.SettingKey == d.Key && a.ConfiguredValue == d.ConfiguredValue))
            .ToList();
        if (pending.Count == 0) return;
        if (!acknowledged) throw new DeviationAcknowledgmentRequiredException(pending);

        var now = _clock.GetUtcNow().UtcDateTime;
        await Context.ComplianceAcknowledgments.AddRangeAsync(pending.Select(d => new ComplianceAcknowledgment
        {
            Scope = scope,
            SettingKey = d.Key,
            SettingLabel = d.Label,
            StatutoryValue = d.StatutoryValue,
            ConfiguredValue = d.ConfiguredValue,
            UserId = actor.UserId,
            UserEmail = actor.Email is { Length: > 256 } email ? email[..256] : actor.Email,
            IpAddress = actor.IpAddress is { Length: > 64 } ip ? ip[..64] : actor.IpAddress,
            AcknowledgedAtUtc = now,
        }), token);
    }

    public async Task<ComplianceAcknowledgmentPage> ListAsync(int page, int pageSize, CancellationToken token = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var query = Context.ComplianceAcknowledgments.AsNoTracking();
        var total = await query.CountAsync(token);
        var items = await query
            .OrderByDescending(a => a.AcknowledgedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ComplianceAcknowledgmentModel
            {
                Id = a.Id,
                Scope = a.Scope,
                SettingKey = a.SettingKey,
                SettingLabel = a.SettingLabel,
                StatutoryValue = a.StatutoryValue,
                ConfiguredValue = a.ConfiguredValue,
                UserEmail = a.UserEmail,
                IpAddress = a.IpAddress,
                AcknowledgedAtUtc = a.AcknowledgedAtUtc,
            })
            .ToListAsync(token);
        return new ComplianceAcknowledgmentPage(items, total, page, pageSize);
    }
}
