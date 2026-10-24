using Hrms.Domain.Entities;

namespace Hrms.Core.Policies.DeductionPolicies;

// What the carrier rule needs to know about one employee in one run. Pure data so the rules below
// are testable without a database.
public sealed record DeductionCarrierInput(
    Guid EmployeeId,
    Guid? HomeClientId,
    // Clients (Guid.Empty = a day with no client) this run pays days for.
    IReadOnlySet<Guid> ClientsInThisRun,
    // Days worked per client (Guid.Empty = no client) across the whole deduction period, in any
    // run, paid or not -- what MostDays and the PrimaryClient fallback rank.
    IReadOnlyDictionary<Guid, int> PeriodDaysByClient,
    // The run that already carries this period, if any other run does.
    Guid? ClaimedByBatchId,
    bool ForceCarry,
    bool ForceSkip);

public sealed record DeductionCarrierDecision(bool Carries, string Reason);

// Decides whether one run takes an employee's deductions (statutory, tax, loans, general other
// income) or pays them earnings-only. The unique PayrollDeductionClaim index is the real guard;
// this only chooses which run should win so the claim is taken by the right one.
public static class DeductionCarrierResolver
{
    public static DeductionCarrierDecision Decide(DeductionCarrierMode mode, DeductionCarrierInput input)
    {
        if (mode == DeductionCarrierMode.EveryRun)
            return new(true, "EveryRun");
        if (input.ClaimedByBatchId != null)
            return new(false, "ClaimedElsewhere");
        if (input.ForceSkip)
            return new(false, "SkippedByOperator");
        if (input.ForceCarry)
            return new(true, "ChosenByOperator");

        return mode switch
        {
            DeductionCarrierMode.Manual => new(false, "NotChosen"),
            DeductionCarrierMode.FirstRunGenerated => new(true, "FirstRun"),
            DeductionCarrierMode.MostDays => ByMostDays(input),
            DeductionCarrierMode.PrimaryClient => ByPrimaryClient(input),
            _ => new(true, "EveryRun"),
        };
    }

    private static DeductionCarrierDecision ByPrimaryClient(DeductionCarrierInput input)
    {
        // Their own client, when they actually worked it this period; otherwise rank by days so a
        // reliever who never reached their home client still gets deducted somewhere.
        if (input.HomeClientId is { } home && input.PeriodDaysByClient.TryGetValue(home, out var days) && days > 0)
            return new(input.ClientsInThisRun.Contains(home), "PrimaryClient");
        var fallback = ByMostDays(input);
        return fallback with { Reason = "PrimaryClientNotWorked:" + fallback.Reason };
    }

    private static DeductionCarrierDecision ByMostDays(DeductionCarrierInput input)
    {
        // No days at all (a payroll-only employee): nothing to rank, whichever run pays them takes it.
        if (input.PeriodDaysByClient.Count == 0)
            return new(true, "NoDays");
        // Ties break on the lowest client id so every run reaches the same answer.
        var top = input.PeriodDaysByClient
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key)
            .First().Key;
        return new(input.ClientsInThisRun.Contains(top), "MostDays");
    }
}
