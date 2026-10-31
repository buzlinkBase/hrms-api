using DTR.Core;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.Compliance;

// A setting in effect today that differs from the DOLE defaults but has no acknowledgment on
// record -- typically saved before acknowledgments were asked for, or while the Terms weren't live.
// Sources names where it is set (the company policy, a client, a shift, an employee...).
public sealed class OutstandingDeviationModel
{
    public string Scope { get; set; } = string.Empty;
    public string SettingKey { get; set; } = string.Empty;
    public string SettingLabel { get; set; } = string.Empty;
    public string StatutoryValue { get; set; } = string.Empty;
    public string ConfiguredValue { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = [];
    public int SourceCount { get; set; }
}

public sealed record OutstandingDeviationsResult(string TermsState, List<OutstandingDeviationModel> Items);

// One deviating setting found in one place.
public sealed record DeviationFinding(string Scope, StatutoryDeviation Deviation, string Source);

// Strategy: one area of the saved settings, read back and run through the same rules its save
// uses, so what is listed here is exactly what saving that area again would ask about.
public interface IDeviationSource
{
    Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token);
}

// Company Settings > Acknowledged Deviations' "Not confirmed" list: every source's findings
// (Composite) minus what has already been acknowledged (same scope, setting and value).
// ConfirmAllAsync lets an admin acknowledge them in one go.
public class OutstandingDeviationService : BaseService<ComplianceAcknowledgment>
{
    private const int MaxSourcesListed = 10;

    private readonly ComplianceAcknowledgmentService _compliance;
    private readonly ITermsLiveness _terms;
    private readonly IReadOnlyList<IDeviationSource> _sources;

    // The sources are registered in DI (one per area; hrms-api adds the company policy one, which
    // needs DTR.Core), so a new area is one more IDeviationSource, with nothing to change here.
    public OutstandingDeviationService(
        IUnitOfWorkService uow,
        ComplianceAcknowledgmentService compliance,
        ITermsLiveness terms,
        IEnumerable<IDeviationSource> sources) : base(uow)
    {
        _compliance = compliance;
        _terms = terms;
        _sources = sources.ToList();
    }

    public async Task<OutstandingDeviationsResult> ListAsync(CancellationToken token = default)
    {
        var state = await _terms.GetStateAsync(token);
        return new OutstandingDeviationsResult(state.ToString(), await OutstandingAsync(token));
    }

    // Records an acknowledgment for everything currently outstanding. Like any save, nothing is
    // recorded while the Terms aren't live. Doesn't commit: the caller does.
    public async Task<int> ConfirmAllAsync(ComplianceActor actor, CancellationToken token = default)
    {
        if (!await _terms.IsLiveAsync(token)) return 0;
        var outstanding = await OutstandingAsync(token);
        foreach (var scope in outstanding.GroupBy(o => o.Scope))
        {
            var deviations = scope
                .Select(o => new StatutoryDeviation(o.SettingKey, o.SettingLabel, o.StatutoryValue, o.ConfiguredValue, o.Note))
                .ToList();
            await _compliance.RecordDeviationsAsync(scope.Key, deviations, actor, token);
        }
        return outstanding.Count;
    }

    private async Task<List<OutstandingDeviationModel>> OutstandingAsync(CancellationToken token)
    {
        var findings = new List<DeviationFinding>();
        foreach (var source in _sources)
            findings.AddRange(await source.FindAsync(token));

        var acknowledged = (await Context.ComplianceAcknowledgments.AsNoTracking()
                .Select(a => new { a.Scope, a.SettingKey, a.ConfiguredValue })
                .ToListAsync(token))
            .Select(a => (a.Scope, a.SettingKey, a.ConfiguredValue))
            .ToHashSet();

        return findings
            .Where(f => !acknowledged.Contains((f.Scope, f.Deviation.Key, f.Deviation.ConfiguredValue)))
            .GroupBy(f => (f.Scope, f.Deviation.Key, f.Deviation.ConfiguredValue))
            .Select(g =>
            {
                var first = g.First().Deviation;
                var sources = g.Select(f => f.Source).Distinct().ToList();
                return new OutstandingDeviationModel
                {
                    Scope = g.Key.Scope,
                    SettingKey = first.Key,
                    SettingLabel = first.Label,
                    StatutoryValue = first.StatutoryValue,
                    ConfiguredValue = first.ConfiguredValue,
                    Note = first.Note,
                    Sources = sources.Take(MaxSourcesListed).ToList(),
                    SourceCount = sources.Count,
                };
            })
            .OrderBy(o => o.Scope != ComplianceAcknowledgmentService.CompanyScope)
            .ThenBy(o => o.SettingLabel)
            .ToList();
    }
}

public static class DeviationFindings
{
    public static IEnumerable<DeviationFinding> At(string scope, string source, IEnumerable<StatutoryDeviation> deviations) =>
        deviations.Select(d => new DeviationFinding(scope, d, source));
}

public sealed class CompanyRateDeviationSource(IUnitOfWorkService uow) : IDeviationSource
{
    private readonly HrmsContext _context = uow.Context;

    public async Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token)
    {
        var rates = await _context.PremiumRates.AsNoTracking().Select(r => new { r.Type, r.Rate }).ToListAsync(token);
        var byType = rates.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Last().Rate);
        return DeviationFindings.At(ComplianceAcknowledgmentService.CompanyScope, "Rate Multipliers",
            StatutoryDeviationRules.Evaluate(new RateSubject(byType)));
    }
}

// Each client's own overrides: retirement accrual, statutory caps and OT rule, and rate multipliers.
public sealed class ClientDeviationSource(IUnitOfWorkService uow, GeneralSettingService settings) : IDeviationSource
{
    private readonly HrmsContext _context = uow.Context;

    public async Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token)
    {
        var clients = await _context.Clients.AsNoTracking()
            .Select(c => new { c.Id, c.Name, c.RetirementDaysPerYear })
            .ToListAsync(token);
        if (clients.Count == 0) return [];

        var policies = await settings.GetSettingsAsync("Client", clients.Select(c => c.Id.ToString()).ToHashSet());
        var rates = (await _context.ClientRateTables.AsNoTracking()
                .Select(r => new { r.ClientId, r.Type, r.Rate })
                .ToListAsync(token))
            .ToLookup(r => r.ClientId);

        var findings = new List<DeviationFinding>();
        foreach (var client in clients)
        {
            var policy = policies.TryGetValue(new SettingGroupKey(client.Id), out var p) ? p : new Dictionary<string, GeneralSettingModel>();
            decimal? Cap(SettingKey key) => policy.TryGetValue(key.ToString(), out var v)
                ? GeneralSettingsUtil.ParsePositiveDecimalOrNull(v.Value)
                : null;
            OvertimeEligibilityRule? otRule = policy.TryGetValue(SettingKey.OTEligibility.ToString(), out var ot)
                && Enum.TryParse<OvertimeEligibilityRule>(ot.Value, true, out var rule) ? rule : null;
            var byType = rates[client.Id].GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Last().Rate);

            findings.AddRange(DeviationFindings.At(ComplianceAcknowledgmentService.ClientScope(client.Id), $"Client: {client.Name}",
            [
                .. StatutoryDeviationRules.Evaluate(new ClientSubject(client.RetirementDaysPerYear)),
                .. StatutoryDeviationRules.Evaluate(new StatutoryCapSubject(
                    Cap(SettingKey.MaxSSSCapping), Cap(SettingKey.MaxPhilHealthCapping), Cap(SettingKey.MaxPagIbigCapping))),
                .. StatutoryDeviationRules.Evaluate(new OvertimeEligibilitySubject(otRule)),
                .. StatutoryDeviationRules.Evaluate(new RateSubject(byType)),
            ]));
        }
        return findings;
    }
}

public sealed class TimeShiftDeviationSource(IUnitOfWorkService uow) : IDeviationSource
{
    private readonly HrmsContext _context = uow.Context;

    public async Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token)
    {
        var shifts = await _context.TimeShifts.AsNoTracking()
            .Select(s => new { s.ShiftName, s.OverTimeThreshold, s.MaxOvertimeHours, s.NightDiffThreshold, s.WithAMBreak, s.WithPMBreak })
            .ToListAsync(token);
        return shifts.SelectMany(s => DeviationFindings.At(ComplianceAcknowledgmentService.CompanyScope, $"Shift: {s.ShiftName}",
            StatutoryDeviationRules.Evaluate(new TimeShiftSubject(
                s.OverTimeThreshold, s.MaxOvertimeHours, s.NightDiffThreshold, s.WithAMBreak, s.WithPMBreak))));
    }
}

public sealed class LeaveDeviationSource(IUnitOfWorkService uow) : IDeviationSource
{
    private readonly HrmsContext _context = uow.Context;

    public async Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token)
    {
        var leaves = await _context.Leaves.AsNoTracking()
            .Select(l => new { l.Code, l.AccrualBasis, l.Credits, l.AccrualRate, l.MinServiceMonths, l.ConvertToCash, l.CashConversionRate })
            .ToListAsync(token);
        return leaves.SelectMany(l => DeviationFindings.At(ComplianceAcknowledgmentService.CompanyScope, $"Leave type: {l.Code}",
            StatutoryDeviationRules.Evaluate(new LeaveSubject(
                l.Code, l.AccrualBasis, l.Credits, l.AccrualRate, l.MinServiceMonths, l.ConvertToCash, l.CashConversionRate))));
    }
}

// Current (not resigned) employees' pay setup, with the same regional minimum wage the employee save uses.
public sealed class EmployeePayDeviationSource(IUnitOfWorkService uow, EmployeePayDeviationService employeePay) : IDeviationSource
{
    private readonly HrmsContext _context = uow.Context;

    public async Task<IEnumerable<DeviationFinding>> FindAsync(CancellationToken token)
    {
        var employees = await _context.Employees.AsNoTracking()
            .Where(e => e.DateResigned == null)
            .Select(e => new
            {
                e.EmployeeNo,
                e.FirstName,
                e.LastName,
                Check = new EmployeePayCheck(
                    e.BranchId,
                    null,
                    e.DailyRate,
                    e.SSSRate == null ? (ComputationBasis?)null : e.SSSRate.ComputationType,
                    e.PHICRate == null ? (ComputationBasis?)null : e.PHICRate.ComputationType,
                    e.HDMFRate == null ? (ComputationBasis?)null : e.HDMFRate.ComputationType,
                    e.TaxRate == null ? (ComputationBasis?)null : e.TaxRate.ComputationType,
                    e.Settings == null ? null : new EmployeeEligibility(
                        e.Settings.IsEligibleForOvertime,
                        e.Settings.IsEligibleForRegularHolidayPay,
                        e.Settings.IsEligibleForNightDifferential,
                        e.Settings.IsEligibleForLeaveCredits,
                        e.Settings.IsEligibleFor13thMonth)),
            })
            .ToListAsync(token);
        if (employees.Count == 0) return [];

        var subjects = await employeePay.SubjectsAsync(employees.Select(e => e.Check).ToList(), token);
        return employees.Zip(subjects).SelectMany(pair =>
            DeviationFindings.At(ComplianceAcknowledgmentService.CompanyScope,
                $"Employee: {pair.First.LastName}, {pair.First.FirstName} ({pair.First.EmployeeNo})",
                StatutoryDeviationRules.Evaluate(pair.Second)));
    }
}
