using System.Text.Json;
using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// The measurable facts of a request that a workflow can route on: how many days, how many hours, how
/// much money, and (for leave) which leave type. Captured when the request is filed and refreshed when
/// it is edited, then kept on the approval instance so routing stays stable however the request or the
/// workflow template changes afterwards.
/// </summary>
public sealed record ApprovalRequestAttributes(
    decimal? Days = null,
    decimal? Hours = null,
    decimal? Amount = null,
    Guid? LeaveTypeId = null)
{
    public decimal? Get(ApprovalStepConditionMetric metric) => metric switch
    {
        ApprovalStepConditionMetric.Days => Days,
        ApprovalStepConditionMetric.Hours => Hours,
        ApprovalStepConditionMetric.Amount => Amount,
        _ => null,
    };

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>The stored snapshot, or null when there is none (a request filed before routing existed) or it can't be read.</summary>
    public static ApprovalRequestAttributes? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ApprovalRequestAttributes>(json); }
        catch (JsonException) { return null; }
    }

    // ---- one factory per request type: how its fields map to the measures ----

    /// <summary>Calendar days covered, halved for a half-day leave.</summary>
    public static ApprovalRequestAttributes ForLeave(DateOnly from, DateOnly to, DayFraction fraction, Guid leaveTypeId)
    {
        var days = (decimal)(to.DayNumber - from.DayNumber + 1);
        return new(Days: fraction == DayFraction.FullDay ? days : days / 2m, LeaveTypeId: leaveTypeId);
    }

    /// <summary>Hours of overtime: the manual entry, else the span between start and end.</summary>
    public static ApprovalRequestAttributes ForOvertime(double manualMinutes, DateTime? start, DateTime? end)
    {
        var minutes = manualMinutes > 0
            ? manualMinutes
            : start.HasValue && end.HasValue ? Math.Max(0, (end.Value - start.Value).TotalMinutes) : 0;
        return new(Hours: Math.Round((decimal)minutes / 60m, 2));
    }

    public static ApprovalRequestAttributes ForOfficialBusiness(DateOnly start, DateOnly end, double cost) =>
        new(Days: end.DayNumber - start.DayNumber + 1, Amount: (decimal)cost);

    /// <summary>Hours out of the office; an open pass slip (not yet returned) counts as zero until it is.</summary>
    public static ApprovalRequestAttributes ForPassSlip(DateTime departure, DateTime? returned) =>
        new(Hours: returned.HasValue ? Math.Round((decimal)Math.Max(0, (returned.Value - departure).TotalHours), 2) : 0m);

    public static ApprovalRequestAttributes ForLoan(decimal principal) => new(Amount: principal);
}

/// <summary>When a step applies: always, or only when a measure of the request passes a threshold.</summary>
public static class ApprovalStepCondition
{
    /// <summary>
    /// Whether the step is part of this request's route. A step with no condition always is. When the
    /// request's value isn't known (filed before routing existed) the step applies -- asking for an
    /// approval is the safe side.
    /// </summary>
    public static bool Applies(ApprovalWorkflowStep step, ApprovalRequestAttributes? attributes)
    {
        if (step.ConditionMetric == ApprovalStepConditionMetric.None) return true;
        if (attributes?.Get(step.ConditionMetric) is not { } actual || step.ConditionValue is not { } threshold) return true;

        return step.ConditionOperator switch
        {
            ApprovalConditionOperator.GreaterOrEqual => actual >= threshold,
            ApprovalConditionOperator.GreaterThan => actual > threshold,
            ApprovalConditionOperator.LessOrEqual => actual <= threshold,
            ApprovalConditionOperator.LessThan => actual < threshold,
            _ => true,
        };
    }

    /// <summary>The measures a request type actually has -- what a step on that type may be conditioned on.</summary>
    public static bool IsOfferedFor(ApprovalStepConditionMetric metric, ApprovalApplicationType type) => metric switch
    {
        ApprovalStepConditionMetric.None => true,
        ApprovalStepConditionMetric.Days => type is ApprovalApplicationType.Leave or ApprovalApplicationType.OfficialBusiness,
        ApprovalStepConditionMetric.Hours => type is ApprovalApplicationType.Overtime or ApprovalApplicationType.PassSlip,
        ApprovalStepConditionMetric.Amount => type is ApprovalApplicationType.Loan or ApprovalApplicationType.OfficialBusiness,
        _ => false,
    };
}
