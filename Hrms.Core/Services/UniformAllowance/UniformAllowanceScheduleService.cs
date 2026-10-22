using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.UniformAllowance;

/// <summary>
/// When and how uniform allowance is credited, for people to read: a client's next credit dates,
/// the credit log (credited, capped and skipped decisions with their reasons), one employee's
/// history and upcoming credits, and the employee portal's "My Uniform Allowance".
/// </summary>
public class UniformAllowanceScheduleService
{
    private const int HistoryLimit = 36;
    private const int CreditLogLimit = 5000;

    private readonly IUnitOfWorkService _uow;
    private readonly UniformAllowancePolicyService _policies;

    public UniformAllowanceScheduleService(IUnitOfWorkService uow, UniformAllowancePolicyService policies)
    {
        _uow = uow;
        _policies = policies;
    }

    private HrmsContext Context => _uow.Context;

    /// <summary>
    /// The client's next credit dates from <paramref name="asOf"/>, with the rules in force on each
    /// (a scheduled change shows from its start). Anniversary credits differ per employee, so they
    /// have none here.
    /// </summary>
    public async Task<List<UniformCreditPreviewModel>> PreviewAsync(Guid clientId, DateOnly asOf, int count, CancellationToken token)
    {
        var client = await Context.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, token)
            ?? throw new NotFoundException("Client not found.");
        var versions = await _policies.VersionsAsync(clientId, token);

        UniformPolicySnapshot? PolicyOn(DateOnly day) =>
            UniformAllowancePolicyResolver.Resolve(versions, client, day) is { Frequency: not UniformCreditFrequency.HireAnniversary } p
                ? p
                : null;

        return UniformCreditSchedules.Upcoming(PolicyOn, asOf, DateOnly.MinValue, Math.Clamp(count, 1, 12))
            .Select(x => Preview<UniformCreditPreviewModel>(x.Date, x.Policy))
            .ToList();
    }

    public async Task<List<UniformAllowanceCreditLogModel>> GetCreditLogAsync(UniformAllowanceCreditLogFilter filter, CancellationToken token)
    {
        var query = Context.UniformAllowanceCreditLogs.AsNoTracking();
        if (filter.From is { } from) query = query.Where(x => x.CreditDate >= from);
        if (filter.To is { } to) query = query.Where(x => x.CreditDate <= to);
        if (filter.ClientId is { } clientId) query = query.Where(x => x.ClientId == clientId);
        if (filter.EmployeeId is { } employeeId) query = query.Where(x => x.EmployeeId == employeeId);
        if (filter.Outcome is { } outcome) query = query.Where(x => x.Outcome == outcome);

        var rows = await query
            .OrderByDescending(x => x.CreditDate).ThenBy(x => x.EmployeeId)
            .Take(CreditLogLimit)
            .ToListAsync(token);
        return await ToModelsAsync(rows, token);
    }

    /// <summary>One employee's rules, last decisions and next credits (Payroll Reports > Uniform Allowance Ledger).</summary>
    public async Task<UniformAllowanceScheduleModel> GetEmployeeScheduleAsync(Guid employeeId, int upcoming, CancellationToken token)
    {
        var model = new UniformAllowanceScheduleModel();
        await FillScheduleAsync(model, employeeId, upcoming, token);
        return model;
    }

    /// <summary>The employee portal's view: the same schedule plus every ledger movement.</summary>
    public async Task<MyUniformAllowanceModel> GetMineAsync(Guid employeeId, CancellationToken token)
    {
        var model = new MyUniformAllowanceModel();
        await FillScheduleAsync(model, employeeId, 6, token);
        model.Entries = (await Context.UniformAllowanceLedgers.AsNoTracking()
                .Where(x => x.EmployeeId == employeeId)
                .OrderBy(x => x.EntryDate).ThenBy(x => x.CreatedAt)
                .ToListAsync(token))
            .Select(x => new LedgerEntryModel
            {
                EntryDate = x.EntryDate,
                EntryType = x.EntryType.ToString(),
                Amount = x.Amount,
                Balance = x.Balance,
                Remarks = x.Particulars,
            })
            .ToList();
        return model;
    }

    private async Task FillScheduleAsync(UniformAllowanceScheduleModel model, Guid employeeId, int upcoming, CancellationToken token)
    {
        var employee = await Context.Employees.AsNoTracking()
            .Where(x => x.Id == employeeId)
            .Select(x => new { x.Id, x.ClientId, x.HireDate, x.DateResigned, x.EmploymentStatus })
            .FirstOrDefaultAsync(token)
            ?? throw new NotFoundException("Employee not found.");
        var fund = await Context.UniformAllowanceFunds.AsNoTracking().FirstOrDefaultAsync(x => x.EmployeeId == employeeId, token);

        model.EmployeeId = employeeId;
        model.Balance = fund?.Balance ?? 0;
        model.ServiceStartDate = fund?.ServiceStartDate ?? employee.HireDate;
        model.SeparatedOn = fund?.SeparatedOn;

        var history = await Context.UniformAllowanceCreditLogs.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.CreditDate)
            .Take(HistoryLimit)
            .ToListAsync(token);
        model.History = await ToModelsAsync(history, token);

        if (employee.ClientId is not { } clientId) return;
        var client = await Context.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientId, token);
        if (client == null) return;

        var versions = await _policies.VersionsAsync(clientId, token);
        var today = PhTime.Today(DateTime.UtcNow);
        var current = UniformAllowancePolicyResolver.Resolve(versions, client, today);
        if (current != null)
        {
            model.Policy = UniformPolicyModels.FromSnapshot(current, clientId);
            model.Policy.ChangeReason = versions.FirstOrDefault(v => v.Id == current.PolicyId)?.ChangeReason
                                        ?? model.Policy.ChangeReason;
        }

        var serviceStart = model.ServiceStartDate.Value;
        var decided = history.Select(h => h.CreditDate).ToHashSet();
        model.Upcoming = UniformCreditSchedules
            .Upcoming(day => UniformAllowancePolicyResolver.Resolve(versions, client, day), today, serviceStart, upcoming + 1)
            .Where(x => !decided.Contains(x.Date))
            .Take(Math.Clamp(upcoming, 1, 12))
            .Select(x =>
            {
                var item = Preview<UniformUpcomingCreditModel>(x.Date, x.Policy);
                item.Blocker = Blocker(x.Policy, x.Date, employee.EmploymentStatus, employee.DateResigned, serviceStart, model.Balance);
                return item;
            })
            .ToList();
    }

    // What would stop this credit on what's known today -- attendance can't be known in advance,
    // so the present-days requirement is assumed met (it's stated in the condition instead).
    internal static string Blocker(
        UniformPolicySnapshot policy, DateOnly date, EmploymentStatus status, DateTime? dateResigned,
        DateOnly serviceStart, decimal balance)
    {
        var candidate = new UniformCreditCandidate(
            policy, status, UniformSeparation.IsSeparated(status, dateResigned, date), serviceStart, date,
            policy.MinPresentDays ?? 0);
        if (UniformCreditDecider.FirstIneligibility(candidate) is { } skip) return skip.Text;
        return policy.MaxBalance is { } cap && balance >= cap ? $"Balance already at the ₱{cap:N2} cap" : string.Empty;
    }

    private static T Preview<T>(DateOnly date, UniformPolicySnapshot policy) where T : UniformCreditPreviewModel, new()
    {
        var (from, to) = UniformCreditSchedules.For(policy.Frequency).PeriodFor(date, policy);
        return new T
        {
            CreditDate = date,
            PeriodStart = from,
            PeriodEnd = to,
            PolicyId = policy.PolicyId,
            Amount = policy.AmountMode == UniformAmountMode.FixedPerPeriod ? policy.Amount : null,
            Condition = Condition(policy),
        };
    }

    /// <summary>What a credit depends on, in a phrase: "if at least 20 present days; after 3 months of service".</summary>
    internal static string Condition(UniformPolicySnapshot p)
    {
        var parts = new List<string>();
        switch (p.AmountMode)
        {
            case UniformAmountMode.ProratedByPresentDays:
                parts.Add($"₱{p.Amount:N2} × present days ÷ {p.ExpectedDaysPerPeriod} (at most ₱{p.Amount:N2})");
                break;
            case UniformAmountMode.PerPresentDay:
                parts.Add($"₱{p.Amount:N2} per present day");
                break;
        }
        if (p.MinPresentDays is { } days) parts.Add($"if at least {days} present days");
        if (p.WaitingMonths > 0) parts.Add($"after {p.WaitingMonths} month{(p.WaitingMonths == 1 ? "" : "s")} of service");
        return string.Join("; ", parts);
    }

    private async Task<List<UniformAllowanceCreditLogModel>> ToModelsAsync(List<UniformAllowanceCreditLog> rows, CancellationToken token)
    {
        var employeeIds = rows.Select(x => x.EmployeeId).Distinct().ToList();
        var clientIds = rows.Select(x => x.ClientId).Distinct().ToList();
        var employees = await Context.Employees.AsNoTracking()
            .Where(x => employeeIds.Contains(x.Id))
            .Select(x => new { x.Id, x.EmployeeNo, x.FirstName, x.LastName })
            .ToDictionaryAsync(x => x.Id, token);
        var clients = await Context.Clients.AsNoTracking()
            .Where(x => clientIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, token);

        return rows.Select(x =>
        {
            employees.TryGetValue(x.EmployeeId, out var e);
            return new UniformAllowanceCreditLogModel
            {
                Id = x.Id,
                EmployeeId = x.EmployeeId,
                EmployeeNo = e?.EmployeeNo ?? string.Empty,
                EmployeeName = e == null ? string.Empty : $"{e.LastName}, {e.FirstName}",
                ClientId = x.ClientId,
                ClientName = clients.GetValueOrDefault(x.ClientId) ?? string.Empty,
                PolicyId = x.PolicyId,
                CreditDate = x.CreditDate,
                PeriodStart = x.PeriodStart,
                PeriodEnd = x.PeriodEnd,
                Outcome = x.Outcome,
                ReasonCode = x.ReasonCode,
                ReasonText = x.ReasonText,
                PresentDays = x.PresentDays,
                RequiredDays = x.RequiredDays,
                Amount = x.Amount,
            };
        }).ToList();
    }
}
