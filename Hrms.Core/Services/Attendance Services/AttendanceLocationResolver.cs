namespace Hrms.Core.Services;

/// <summary>Where a punch happened: each part optional.</summary>
public sealed record AttendanceLocation(Guid? BranchId, Guid? ClientId, Guid? OperationAreaId, Guid? DepartmentId)
{
    public static readonly AttendanceLocation None = new(null, null, null, null);
}

/// <summary>
/// The one rule for the branch/client/project site/department stored on an attendance log, used by
/// every way a log gets in (device sync, upload, manual entry, the DTR's "Add time log", tagging an
/// unregistered log, pass slips): whatever the source left blank comes from the employee's own
/// assignment -- without mixing branches. The employee's project site is only taken when it
/// belongs to the branch the punch ends up in; a Cebu punch is never tagged to a Manila site.
/// </summary>
public static class AttendanceLocationResolver
{
    /// <param name="chosen">What the device, upload or person entering the log set.</param>
    /// <param name="employee">The employee's own assignment; null for an unregistered Bio ID.</param>
    /// <param name="branchOfArea">A project site's branch (null when the site has none).</param>
    public static AttendanceLocation Resolve(AttendanceLocation chosen, AttendanceLocation? employee, Func<Guid, Guid?> branchOfArea)
    {
        var own = employee ?? AttendanceLocation.None;
        var clientId = chosen.ClientId ?? own.ClientId;
        var departmentId = chosen.DepartmentId ?? own.DepartmentId;

        // A chosen site is taken as given, and places the punch in its branch when none was chosen.
        if (chosen.OperationAreaId is { } chosenArea)
            return new(chosen.BranchId ?? branchOfArea(chosenArea) ?? own.BranchId, clientId, chosenArea, departmentId);

        var branchId = chosen.BranchId ?? own.BranchId;
        var areaId = own.OperationAreaId is { } ownArea && BelongsTo(ownArea, branchId, chosen.BranchId, branchOfArea)
            ? ownArea
            : (Guid?)null;
        return new(branchId, clientId, areaId, departmentId);
    }

    // The employee's site fits unless a branch was chosen for the punch and the site sits in a
    // different one (a site with no branch fits anywhere).
    private static bool BelongsTo(Guid area, Guid? branchId, Guid? chosenBranchId, Func<Guid, Guid?> branchOfArea)
    {
        if (chosenBranchId == null) return true;
        var areaBranch = branchOfArea(area);
        return areaBranch == null || areaBranch == branchId;
    }

    /// <summary>The branch of each given project site, in one query -- feed to <see cref="Resolve"/>.</summary>
    public static async Task<Func<Guid, Guid?>> LoadBranchOfAreaAsync(HrmsContext context, IEnumerable<Guid?> areaIds, CancellationToken token)
    {
        var ids = areaIds.Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        if (ids.Count == 0) return _ => null;
        var branches = await context.Areas.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.BranchId })
            .ToDictionaryAsync(x => x.Id, x => x.BranchId, token);
        return id => branches.GetValueOrDefault(id);
    }
}
