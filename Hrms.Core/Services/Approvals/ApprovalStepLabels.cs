using Hrms.Domain.Entities.Approvals;

namespace Hrms.Core.Services.Approvals;

/// <summary>
/// The words the approval timeline uses for a dynamic approver step: who it means ("Your Branch
/// Head"), plus the person it resolved to for this applicant, or a note when nobody could be found.
/// <c>subject</c> null reads "Your ..." (the applicant's view); "Applicant" reads "Applicant's ...".
/// </summary>
public static class ApprovalStepLabels
{
    public static string? Dynamic(ApprovalWorkflowStep step, string? resolvedName, bool unresolved, string? subject = null)
    {
        var level = step.ApproverLevel ?? 1;
        var whose = Possessive(subject);
        var role = step.ApproverType switch
        {
            ApproverType.ApplicantManager => $"{whose} Manager",
            ApproverType.ApplicantManagerLevel => level <= 1 ? $"{whose} Manager" : $"{whose} Manager {level} Levels Up",
            ApproverType.ApplicantDepartmentHead => level <= 1 ? $"{whose} Department Head" : $"Department Head {level - 1} Level{(level == 2 ? "" : "s")} Up",
            ApproverType.ApplicantSectionHead => $"{whose} Section Head",
            ApproverType.ApplicantBranchHead => $"{whose} Branch Head",
            ApproverType.ApplicantAreaHead => $"{whose} Project Site Head",
            ApproverType.ApplicantClientHead => $"{whose} Client Head",
            _ => null,
        };
        if (role == null) return null;
        if (unresolved) return $"{role} (no one assigned)";
        return string.IsNullOrWhiteSpace(resolvedName) ? role : $"{role}: {resolvedName}";
    }

    public static string Possessive(string? subject) => subject == null ? "Your" : $"{subject}'s";
}
