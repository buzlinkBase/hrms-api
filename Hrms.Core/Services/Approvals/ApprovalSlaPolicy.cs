using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

/// <summary>What the sweep should do about one waiting request right now.</summary>
public sealed record SlaDecision(bool Remind, bool Escalate)
{
    public static readonly SlaDecision Nothing = new(false, false);
    public bool Any => Remind || Escalate;
}

/// <summary>
/// The reminder / escalation timing rules for a step, with no I/O so they are testable on their own.
/// A step can remind its approvers once it has been waiting RemindAfterHours (and again every
/// RemindRepeatHours, up to <see cref="MaxReminders"/>), and escalate to a named person once it has
/// waited EscalateAfterHours. Each fires from the time the step was entered, and the sweep records
/// what it did on the instance, so running it often never repeats itself.
/// </summary>
public static class ApprovalSlaPolicy
{
    /// <summary>A step never reminds more than this many times, however long it waits.</summary>
    public const int MaxReminders = 10;

    public static bool HasSla(ApprovalWorkflowStep step) =>
        step.RemindAfterHours is > 0 || (step.EscalateAfterHours is > 0 && step.EscalateToEmployeeId.HasValue);

    /// <summary>
    /// When the request reached its current step: the recorded time, else the last approval of the
    /// current cycle, else when it was filed (requests that predate the timer).
    /// </summary>
    public static DateTime StepEnteredAt(ApprovalInstance instance)
    {
        if (instance.StepEnteredAt is { } entered) return entered;

        var lastApproval = instance.Actions
            .Where(a => a.CycleNumber == instance.CycleNumber && a.Action == ApprovalActionType.Approved)
            .Select(a => (DateTime?)a.CreatedAt)
            .Max();
        return lastApproval ?? instance.CreatedAt;
    }

    public static SlaDecision Evaluate(ApprovalInstance instance, ApprovalWorkflowStep step, DateTime nowUtc)
    {
        var waited = nowUtc - StepEnteredAt(instance);
        if (waited <= TimeSpan.Zero) return SlaDecision.Nothing;

        var escalate = step.EscalateAfterHours is > 0 && step.EscalateToEmployeeId.HasValue
            && instance.EscalatedToEmployeeId is null
            && waited >= TimeSpan.FromHours(step.EscalateAfterHours.Value);

        var remind = false;
        if (step.RemindAfterHours is > 0 && instance.ReminderCount < MaxReminders)
        {
            if (instance.ReminderCount == 0)
                remind = waited >= TimeSpan.FromHours(step.RemindAfterHours.Value);
            else if (step.RemindRepeatHours is > 0 && instance.LastReminderAt is { } last)
                remind = nowUtc - last >= TimeSpan.FromHours(step.RemindRepeatHours.Value);
        }

        return new SlaDecision(remind, escalate);
    }

    /// <summary>"3 days", "1 day 4 hours" or "5 hours" -- how long a request has waited, for the notice.</summary>
    public static string DescribeWait(TimeSpan waited)
    {
        var days = (int)waited.TotalDays;
        var hours = waited.Hours;
        if (days <= 0) return $"{Math.Max(1, (int)waited.TotalHours)} hour{((int)waited.TotalHours <= 1 ? "" : "s")}";
        var dayText = $"{days} day{(days == 1 ? "" : "s")}";
        return hours > 0 ? $"{dayText} {hours} hour{(hours == 1 ? "" : "s")}" : dayText;
    }
}
