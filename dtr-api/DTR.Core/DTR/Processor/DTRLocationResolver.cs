using Hrms.Domain.Entities;

namespace DTR.Core;

public sealed record DTRLocation(Guid? BranchId, Guid? ClientId, Guid? AreaId);

/// <summary>
/// Where a day's DTR is recorded. A project site tagged on the day's Work Rotation Plan wins
/// over the first punch's site and the employee's own, and brings its branch/client with it
/// (whatever the site leaves blank still comes from the punch, then the employee). Untagged
/// days keep the punch-then-employee rule.
/// </summary>
public static class DTRLocationResolver
{
    public static DTRLocation Resolve(CurrentShift? shift, Attendance? firstPunch, EmployeeDTRRun? employee)
    {
        var branchId = firstPunch?.BranchId ?? employee?.BranchId;
        var clientId = firstPunch?.ClientId ?? employee?.ClientId;

        if (shift?.AreaId is { } scheduledArea)
        {
            return new(shift.AreaBranchId ?? branchId, shift.AreaClientId ?? clientId, scheduledArea);
        }

        return new(branchId, clientId, firstPunch?.OperationAreaId ?? employee?.AreaId);
    }
}
