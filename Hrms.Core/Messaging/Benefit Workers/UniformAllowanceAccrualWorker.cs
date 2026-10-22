using Hrms.Core.Services;
using Hrms.Core.Services.UniformAllowance;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Hrms.Core.Messaging.BenefitWorkers;

// Runs every day as a step of DailyBenefitsWorker (the Quartz-scheduled daily benefits run). For
// each client, the uniform allowance rules in force that day (UniformAllowancePolicyResolver: a
// saved policy version, or the legacy Client columns) decide, per employee:
//   1. separation and rehire -- a separation is remembered on the fund (forfeiting the balance if
//      the policy says so); a return moves the service start unless the policy bridges the gap;
//   2. expiry -- a yearly reset or rolling expiry lapses old balance;
//   3. the credit, when the policy's schedule says one is due today: the eligibility chain, the
//      amount rule and the cap (UniformCreditDecider), recorded as a UniformAllowanceCreditLog row
//      whatever the outcome, plus an Accrual ledger row when something is credited.
//
// Idempotency: one credit-log row per employee per day (unique index) -- a re-run skips anyone
// already decided; Accrual ledger rows also carry a per-month dedupe key, and Expiry rows one per
// month. Each employee's writes are saved on their own (UniformAllowanceFundService) so one
// collision can't undo another employee's credit, and the run commits once at the end --
// BuzlinkRepository's UnitOfWork commits only once per instance. A failure before then rolls the
// whole day back, and the retried message decides it again from scratch.
public class UniformAllowanceAccrualWorker : IConsumer<RunUniformAllowanceAccrual>
{
    private readonly IUnitOfWorkService _uow;
    private readonly UniformAllowanceFundService _fundService;
    private readonly DailyRecordService _dailyRecordService;
    private readonly ILogger<UniformAllowanceAccrualWorker> _logger;

    public UniformAllowanceAccrualWorker(
        IUnitOfWorkService uow,
        UniformAllowanceFundService fundService,
        DailyRecordService dailyRecordService,
        ILogger<UniformAllowanceAccrualWorker> logger)
    {
        _uow = uow;
        _fundService = fundService;
        _dailyRecordService = dailyRecordService;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<RunUniformAllowanceAccrual> context) =>
        RunAsync(context.Message, context.CancellationToken);

    private sealed record StaffMember(
        Guid Id, Guid ClientId, DateOnly HireDate, DateTime? DateResigned, EmploymentStatus Status);

    private sealed record DueCredit(StaffMember Member, UniformPolicySnapshot Policy, DateOnly ServiceStart, DateOnly From, DateOnly To);

    public async Task RunAsync(RunUniformAllowanceAccrual message, CancellationToken token)
    {
        var date = message.ProcessDate;

        var versions = await _uow.Repository.FindAll<UniformAllowancePolicy>().AsNoTracking().ToListAsync(token);
        var configuredClientIds = versions.Select(v => v.ClientId).Distinct().ToList();
        var clients = await _uow.Repository
            .Find<Client>(x => (x.UniformAllowance != null && x.UniformAllowance > 0) || configuredClientIds.Contains(x.Id))
            .AsNoTracking()
            .ToListAsync(token);
        var policies = clients
            .Select(c => (c.Id, Policy: UniformAllowancePolicyResolver.Resolve(versions, c, date)))
            .Where(x => x.Policy is { IsEnabled: true })
            .ToDictionary(x => x.Id, x => x.Policy!);
        if (policies.Count == 0) return;

        var clientIds = policies.Keys.ToList();
        var staff = await _uow.Repository
            .Find<Employee>(x => x.ClientId.HasValue && clientIds.Contains(x.ClientId.Value))
            .AsNoTracking()
            .Select(x => new StaffMember(x.Id, x.ClientId!.Value, x.HireDate, x.DateResigned, x.EmploymentStatus))
            .ToListAsync(token);
        if (staff.Count == 0) return;

        var staffIds = staff.Select(x => x.Id).ToList();
        var funds = await _fundService.GetTrackedFundsAsync(staffIds, token);

        foreach (var member in staff)
            await ApplySeparationAsync(member, policies[member.ClientId], funds.GetValueOrDefault(member.Id), date, token);
        await ApplyExpiryAsync(staff, policies, funds, date, token);

        var credited = await CreditDueAsync(staff, policies, funds, date, token);
        await _uow.CommitChangesAsync("", token);
        _logger.LogInformation("Uniform allowance {Date}: credited {Count} employee(s)", date, credited);
    }

    // A separation is remembered once (forfeiting the balance under a Forfeit policy); a return
    // after one sets where service counts from. Only employees with a fund are tracked -- with no
    // balance and no credits there is nothing to forfeit or bridge.
    private async Task ApplySeparationAsync(
        StaffMember member, UniformPolicySnapshot policy, UniformAllowanceFund? fund, DateOnly date, CancellationToken token)
    {
        if (fund == null) return;
        var separated = UniformSeparation.IsSeparated(member.Status, member.DateResigned, date);

        if (separated && fund.SeparatedOn == null)
        {
            fund.SeparatedOn = UniformSeparation.SeparatedOn(member.DateResigned, date);
            fund.ServiceStartDate ??= member.HireDate;
            if (policy.OnSeparation == UniformSeparationRule.Forfeit)
                _fundService.Forfeit(fund, date, $"Forfeited on separation ({fund.SeparatedOn:MMM d, yyyy})");
            await _fundService.SaveAsync(token);
        }
        else if (!separated && fund.SeparatedOn is { } separatedOn)
        {
            fund.ServiceStartDate = UniformSeparation.ServiceStartAfterRehire(
                policy, fund.ServiceStartDate ?? member.HireDate, member.HireDate, separatedOn, date);
            fund.SeparatedOn = null;
            await _fundService.SaveAsync(token);
        }
    }

    private async Task ApplyExpiryAsync(
        List<StaffMember> staff, Dictionary<Guid, UniformPolicySnapshot> policies,
        Dictionary<Guid, UniformAllowanceFund> funds, DateOnly date, CancellationToken token)
    {
        var expiring = staff
            .Where(m => funds.TryGetValue(m.Id, out var fund) && fund.Balance > 0 &&
                        UniformExpiry.RunsOn(policies[m.ClientId], date))
            .ToList();

        // Rolling expiry needs what was credited since the cut-off -- one query per cut-off date.
        foreach (var group in expiring.GroupBy(m => UniformExpiry.FreshSince(policies[m.ClientId], date)))
        {
            var rolling = group.Where(m => policies[m.ClientId].Expiry == UniformBalanceExpiry.ExpireAfterMonths)
                .Select(m => m.Id).ToList();
            var fresh = await _fundService.FreshCreditsSinceAsync(rolling, group.Key, token);

            foreach (var member in group)
            {
                var policy = policies[member.ClientId];
                var fund = funds[member.Id];
                var lapsed = UniformExpiry.Due(policy, date, fund.Balance, fresh.GetValueOrDefault(member.Id));
                if (lapsed <= 0) continue;
                var particulars = policy.Expiry == UniformBalanceExpiry.ResetAnnually
                    ? $"Yearly reset — {date:MMMM yyyy}"
                    : $"Expired — credited before {group.Key:MMM d, yyyy}";
                await _fundService.ExpireAsync(fund, lapsed, date, particulars, token);
            }
        }
    }

    private async Task<int> CreditDueAsync(
        List<StaffMember> staff, Dictionary<Guid, UniformPolicySnapshot> policies,
        Dictionary<Guid, UniformAllowanceFund> funds, DateOnly date, CancellationToken token)
    {
        var staffIds = staff.Select(x => x.Id).ToList();
        var alreadyDecided = await _uow.Repository
            .Find<UniformAllowanceCreditLog>(x => staffIds.Contains(x.EmployeeId) && x.CreditDate == date)
            .Select(x => x.EmployeeId)
            .ToHashSetAsync(token);
        // Credits from before the credit log existed (or an earlier credit day this month after a
        // policy change) -- at most one credit a month whatever the frequency.
        var creditedThisMonth = await _uow.Repository
            .Find<UniformAllowanceLedger>(x =>
                staffIds.Contains(x.EmployeeId) &&
                x.EntryType == UniformAllowanceEntryType.Accrual &&
                x.EntryDate.Year == date.Year && x.EntryDate.Month == date.Month)
            .Select(x => x.EmployeeId)
            .ToHashSetAsync(token);

        var due = new List<DueCredit>();
        foreach (var member in staff)
        {
            if (alreadyDecided.Contains(member.Id)) continue;
            var policy = policies[member.ClientId];
            var serviceStart = funds.GetValueOrDefault(member.Id)?.ServiceStartDate ?? member.HireDate;
            var schedule = UniformCreditSchedules.For(policy.Frequency);
            if (!schedule.IsDue(date, policy, serviceStart)) continue;
            // Not on the books yet on the credit day.
            if (member.HireDate > date) continue;
            var (from, to) = schedule.PeriodFor(date, policy);
            due.Add(new DueCredit(member, policy, serviceStart, from, to));
        }
        if (due.Count == 0) return 0;

        var presentDays = new Dictionary<Guid, int>();
        foreach (var period in due.Where(d => d.Policy.NeedsAttendance).GroupBy(d => (d.From, d.To)))
        {
            var counts = await _dailyRecordService.CountPresentDaysInRangeBatchAsync(
                period.Select(d => d.Member.Id), period.Key.From, period.Key.To, token);
            foreach (var (employeeId, count) in counts) presentDays[employeeId] = count;
        }

        var credited = 0;
        foreach (var item in due)
        {
            var member = item.Member;
            var days = item.Policy.NeedsAttendance ? presentDays.GetValueOrDefault(member.Id) : (int?)null;
            var log = new UniformAllowanceCreditLog
            {
                EmployeeId = member.Id,
                ClientId = member.ClientId,
                PolicyId = item.Policy.PolicyId,
                CreditDate = date,
                PeriodStart = item.From,
                PeriodEnd = item.To,
                PresentDays = days,
                RequiredDays = item.Policy.MinPresentDays,
            };

            if (creditedThisMonth.Contains(member.Id))
            {
                await RecordSkipAsync(log, new UniformSkip(
                    UniformSkipReason.AlreadyCreditedThisMonth, "Already credited this month"), token);
                continue;
            }

            var fund = funds.GetValueOrDefault(member.Id);
            var decision = UniformCreditDecider.Decide(
                new UniformCreditCandidate(
                    item.Policy, member.Status,
                    UniformSeparation.IsSeparated(member.Status, member.DateResigned, date),
                    item.ServiceStart, date, days ?? 0),
                fund?.Balance ?? 0);

            if (decision.Outcome == UniformCreditOutcome.Skipped)
            {
                await RecordSkipAsync(log, decision.Reason!, token);
                continue;
            }

            if (fund == null)
            {
                fund = _fundService.NewFund(member.Id, member.HireDate);
                funds[member.Id] = fund;
            }
            log.Outcome = decision.Outcome;
            log.Amount = decision.Amount;
            log.ReasonCode = decision.Reason?.Reason.ToString() ?? string.Empty;
            log.ReasonText = decision.Reason?.Text ?? string.Empty;
            if (await _fundService.CreditAsync(fund, log, Particulars(item, days), token))
            {
                credited++;
            }
            else
            {
                _logger.LogWarning(
                    "Duplicate uniform allowance credit skipped for employee {EmployeeId} ({Date}) — already committed by another instance/redelivery",
                    member.Id, date);
            }
        }
        return credited;
    }

    private async Task RecordSkipAsync(UniformAllowanceCreditLog log, UniformSkip skip, CancellationToken token)
    {
        log.Outcome = UniformCreditOutcome.Skipped;
        log.Amount = 0;
        log.ReasonCode = skip.Reason.ToString();
        log.ReasonText = skip.Text;
        await _fundService.RecordSkipAsync(log, token);
    }

    // "Uniform allowance — Sep 2026", "— Jul–Sep 2026" or "— Oct 2025 – Sep 2026", plus the present
    // days when they set the amount.
    internal static string Particulars(DateOnly from, DateOnly to, UniformAmountMode mode, int? presentDays)
    {
        var period = from.Year == to.Year && from.Month == to.Month
            ? $"{from:MMM yyyy}"
            : from.Year == to.Year
                ? $"{from:MMM}–{to:MMM yyyy}"
                : $"{from:MMM yyyy} – {to:MMM yyyy}";
        var days = mode != UniformAmountMode.FixedPerPeriod && presentDays is { } d ? $" ({d} present days)" : string.Empty;
        return $"Uniform allowance — {period}{days}";
    }

    private static string Particulars(DueCredit item, int? presentDays) =>
        Particulars(item.From, item.To, item.Policy.AmountMode, presentDays);
}

public class UniformAllowanceAccrualWorkerDefinition : ConsumerDefinition<UniformAllowanceAccrualWorker>
{
    public UniformAllowanceAccrualWorkerDefinition()
    {
        EndpointName = "hrms-uniform-allowance-accrual-que";
        ConcurrentMessageLimit = 1;
    }
}
