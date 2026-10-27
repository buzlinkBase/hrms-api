using Hrms.Domain.Entities;
using Hrms.Domain.Entities.Approvals;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.Approvals;

public sealed record SlaSweepResult(int Reminded, int Escalated)
{
    public int Total => Reminded + Escalated;
}

// One company's reminder / escalation sweep: every request still waiting on a step that has timing
// set, checked against ApprovalSlaPolicy. Run hourly by the Quartz ApprovalSlaJob (one message per
// company); safe to run as often as wanted since what was done is recorded on each request.
public class ApprovalSlaService
{
    private readonly IUnitOfWorkService _uow;
    private readonly ApprovalEngineService _engine;

    public ApprovalSlaService(IUnitOfWorkService uow, ApprovalEngineService engine)
    {
        _uow = uow;
        _engine = engine;
    }

    public async Task<SlaSweepResult> RunAsync(DateTime nowUtc, CancellationToken token)
    {
        var context = _uow.Context ?? throw new InvalidOperationException("The unit of work has no context.");

        // The sweep acts for no one, so it must see every waiting request whatever its confidentiality.
        var instances = await context.IgnoringConfidentiality<ApprovalInstance>()
            .Where(i => i.Status == ApprovalInstanceStatus.InProgress && i.ApprovalWorkflowId != null &&
                        i.Workflow!.Steps.Any(s => s.RemindAfterHours != null || s.EscalateAfterHours != null))
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.NamedApprovers)
            .Include(i => i.Workflow).ThenInclude(w => w!.Steps).ThenInclude(s => s.Groups)
            .Include(i => i.Actions)
            .AsSplitQuery()
            .ToListAsync(token);
        if (instances.Count == 0) return new SlaSweepResult(0, 0);

        var applicantIds = instances.Select(i => i.ApplicantEmployeeId).Distinct().ToList();
        var applicants = (await context.IgnoringConfidentiality<Employee>()
                .Where(e => applicantIds.Contains(e.Id)).AsNoTracking().ToListAsync(token))
            .ToDictionary(e => e.Id);

        int reminded = 0, escalated = 0;
        foreach (var instance in instances)
        {
            var step = instance.Workflow?.Steps.SingleOrDefault(s => s.StepNumber == instance.CurrentStepNumber);
            if (step == null || !ApprovalSlaPolicy.HasSla(step)) continue;
            if (!applicants.TryGetValue(instance.ApplicantEmployeeId, out var applicant)) continue;

            var decision = ApprovalSlaPolicy.Evaluate(instance, step, nowUtc);
            if (!decision.Any) continue;

            var waited = nowUtc - ApprovalSlaPolicy.StepEnteredAt(instance);
            if (decision.Remind)
            {
                await _engine.SendReminderAsync(instance, applicant, waited, token);
                reminded++;
            }
            if (decision.Escalate)
            {
                await _engine.EscalateAsync(instance, applicant, waited, token);
                escalated++;
            }
        }

        if (reminded + escalated > 0) await _uow.CommitChangesAsync("", token);
        return new SlaSweepResult(reminded, escalated);
    }
}
