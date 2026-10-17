using DTR.Core;
using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services;

// What the portal Coworkers page shows besides birthdays. Every widget is off until the company
// switches it on (CoworkerWidgetSettings), reads go through the normal tenant- and
// confidentiality-filtered queries, only current employees are listed, and the models are slim --
// no pay, DOB, age or ids. Contact details need the employee's own opt-in (ShareContact).
public class CoworkerService
{
    public const int NewHireDays = 30;
    public const int AnniversaryLookAheadDays = 7;
    public const int DirectoryPageSize = 24;
    private const int MaxOrgNodes = 2000;

    private static readonly int[] MilestoneYears = [1, 3, 5, 10, 15, 20, 25, 30, 35, 40];

    private readonly IUnitOfWorkService _uow;
    private readonly ImageUrlResolver _imageUrls;
    private readonly GeneralSettingService _settings;

    public CoworkerService(IUnitOfWorkService uow, ImageUrlResolver imageUrls, GeneralSettingService settings)
    {
        _uow = uow;
        _imageUrls = imageUrls;
        _settings = settings;
    }

    public async Task<CoworkerWidgetSettings> GetWidgetsAsync() =>
        CoworkerWidgetSettings.From(await _settings.GetSettingsAsync("Company"));

    public async Task<List<CoworkerCardModel>> GetNewHiresAsync(DateOnly today, CancellationToken token)
    {
        if (!(await GetWidgetsAsync()).ShowNewHires) return [];

        var since = today.AddDays(-NewHireDays);
        var rows = await ActiveEmployees()
            .Where(e => e.HireDate >= since && e.HireDate <= today)
            .OrderByDescending(e => e.HireDate)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.ProfileImg, Department = e.Department != null ? e.Department.Name : string.Empty, Position = e.Position != null ? e.Position.Name : string.Empty })
            .ToListAsync(token);
        return await ToCardsAsync(rows.Select(r => ToRow(r.Id, r.FirstName, r.LastName, r.ProfileImg, r.Department, r.Position)).ToList(), token);
    }

    public async Task<List<AnniversaryModel>> GetAnniversariesAsync(DateOnly today, CancellationToken token)
    {
        if (!(await GetWidgetsAsync()).ShowAnniversaries) return [];

        var rows = await ActiveEmployees()
            .Where(e => e.HireDate < today)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.ProfileImg, Department = e.Department != null ? e.Department.Name : string.Empty, Position = e.Position != null ? e.Position.Name : string.Empty, e.HireDate })
            .ToListAsync(token);

        var due = rows
            .Select(r => new { Row = ToRow(r.Id, r.FirstName, r.LastName, r.ProfileImg, r.Department, r.Position), r.HireDate, DaysUntil = BirthdayService.DaysUntilNextBirthday(r.HireDate, today) })
            .Where(r => r.DaysUntil <= AnniversaryLookAheadDays)
            .Select(r => new { r.Row, r.DaysUntil, Years = today.AddDays(r.DaysUntil).Year - r.HireDate.Year })
            .Where(r => IsMilestone(r.Years))
            .OrderBy(r => r.DaysUntil).ThenBy(r => r.Row.FullName)
            .ToList();

        var result = new List<AnniversaryModel>(due.Count);
        foreach (var d in due)
        {
            var model = new AnniversaryModel { Years = d.Years, DaysUntil = d.DaysUntil };
            result.Add(await FillAsync(model, d.Row, token));
        }
        return result;
    }

    // Approved leave and travel orders covering today. Only "Out", "Half day" or "Away" is shown --
    // the leave type (sick, maternity...) and travel destination stay private.
    public async Task<List<OutTodayModel>> GetOutTodayAsync(DateOnly today, CancellationToken token)
    {
        if (!(await GetWidgetsAsync()).ShowWhosOut) return [];

        var leaves = await _uow.Repository
            .Find<LeaveApplication>(l => l.ApprovalStatus == ApprovalStatus.Approved
                && l.LeaveDateFrom <= today && l.LeaveDateTo >= today)
            .AsNoTracking()
            .Select(l => new { l.EmployeeId, l.DayFraction, l.DurationType })
            .ToListAsync(token);
        var travels = await _uow.Repository
            .Find<TravelOrderApplication>(t => t.ApprovalStatus == ApprovalStatus.Approved
                && t.StartDate <= today && t.EndDate >= today)
            .AsNoTracking()
            .Select(t => t.EmployeeId)
            .ToListAsync(token);

        var notes = new Dictionary<Guid, string>();
        foreach (var l in leaves)
        {
            var half = l.DurationType == DurationType.Partial || l.DayFraction != DayFraction.FullDay;
            // A full-day leave wins over a half-day one on the same date.
            if (!half) notes[l.EmployeeId] = "Out";
            else notes.TryAdd(l.EmployeeId, "Half day");
        }
        foreach (var id in travels) notes.TryAdd(id, "Away");
        if (notes.Count == 0) return [];

        var ids = notes.Keys.ToList();
        var rows = await ActiveEmployees()
            .Where(e => ids.Contains(e.Id))
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.ProfileImg, Department = e.Department != null ? e.Department.Name : string.Empty, Position = e.Position != null ? e.Position.Name : string.Empty })
            .ToListAsync(token);

        var result = new List<OutTodayModel>(rows.Count);
        foreach (var r in rows)
            result.Add(await FillAsync(new OutTodayModel { Note = notes[r.Id] }, ToRow(r.Id, r.FirstName, r.LastName, r.ProfileImg, r.Department, r.Position), token));
        return result;
    }

    public async Task<DirectoryPageModel> SearchDirectoryAsync(
        string? query, Guid? departmentId, int page, CancellationToken token)
    {
        if (!(await GetWidgetsAsync()).ShowDirectory) return new DirectoryPageModel();

        var q = ActiveEmployees();
        if (departmentId != null) q = q.Where(e => e.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(e => e.FirstName.Contains(term) || e.LastName.Contains(term)
                || (e.Position != null && e.Position.Name.Contains(term)));
        }

        var total = await q.CountAsync(token);
        var rows = await q
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Skip((Math.Max(page, 1) - 1) * DirectoryPageSize)
            .Take(DirectoryPageSize)
            .Select(e => new
            {
                e.Id,
                e.FirstName,
                e.LastName,
                e.ProfileImg,
                Department = e.Department != null ? e.Department.Name : string.Empty,
                Position = e.Position != null ? e.Position.Name : string.Empty,
                Branch = e.Branch != null ? e.Branch.Name : string.Empty,
                e.ShareContact,
                e.Email,
                e.Contact,
            })
            .ToListAsync(token);

        var items = new List<DirectoryEntryModel>(rows.Count);
        foreach (var r in rows)
        {
            items.Add(await FillAsync(new DirectoryEntryModel
            {
                BranchName = r.Branch,
                Email = r.ShareContact ? r.Email : null,
                Contact = r.ShareContact ? r.Contact : null,
            }, ToRow(r.Id, r.FirstName, r.LastName, r.ProfileImg, r.Department, r.Position), token));
        }
        return new DirectoryPageModel { Items = items, Total = total };
    }

    // Reporting lines from ManagerId. Roots are people with no (visible) manager. A cycle in the
    // data can't loop forever: each employee is placed at most once.
    public async Task<List<OrgNodeModel>> GetOrgChartAsync(CancellationToken token)
    {
        if (!(await GetWidgetsAsync()).ShowDirectory) return [];

        var people = await ActiveEmployees()
            .OrderBy(e => e.FirstName).ThenBy(e => e.LastName)
            .Take(MaxOrgNodes)
            .Select(e => new
            {
                e.Id,
                e.ManagerId,
                e.FirstName,
                e.LastName,
                Position = e.Position != null ? e.Position.Name : string.Empty,
            })
            .ToListAsync(token);

        return BuildOrgTree(people.Select(p =>
            (p.Id, p.ManagerId, new OrgNodeModel
            {
                Id = p.Id,
                FullName = $"{p.FirstName} {p.LastName}".Trim(),
                PositionName = p.Position,
            })).ToList());
    }

    public static List<OrgNodeModel> BuildOrgTree(List<(Guid Id, Guid? ManagerId, OrgNodeModel Node)> people)
    {
        var byId = people.ToDictionary(p => p.Id, p => p.Node);
        var parents = people.ToDictionary(p => p.Id, p => p.ManagerId);
        var roots = new List<OrgNodeModel>();
        foreach (var (id, managerId, node) in people)
        {
            if (managerId != null && managerId != id && byId.ContainsKey(managerId.Value)
                && !ReachesSelf(id, managerId.Value, parents))
                byId[managerId.Value].Reports.Add(node);
            else
                roots.Add(node);
        }
        return roots;
    }

    // True if walking up from `managerId` arrives back at `id` (attaching would close a loop).
    private static bool ReachesSelf(Guid id, Guid managerId, Dictionary<Guid, Guid?> parents)
    {
        var seen = new HashSet<Guid>();
        Guid? current = managerId;
        while (current != null && seen.Add(current.Value))
        {
            if (current == id) return true;
            current = parents.GetValueOrDefault(current.Value);
        }
        return false;
    }

    // Employee-controlled opt-in; touches only the given employee's own row.
    public async Task<bool> SetShareContactAsync(Guid employeeId, bool share, CancellationToken token)
    {
        var employee = await _uow.Repository.Find<Employee>(e => e.Id == employeeId).FirstOrDefaultAsync(token);
        if (employee == null) return false;
        employee.ShareContact = share;
        // The unit of work opens a transaction on creation; only CommitChangesAsync commits it.
        await _uow.CommitChangesAsync("", token);
        return true;
    }

    public static bool IsMilestone(int years) => MilestoneYears.Contains(years);

    private IQueryable<Employee> ActiveEmployees() =>
        _uow.Repository.Find<Employee>(e => e.DateResigned == null).AsNoTracking();

    private sealed record CardRow(Guid Id, string FullName, string ProfileImg, string Department, string Position);

    private static CardRow ToRow(Guid id, string first, string last, string img, string department, string position) =>
        new(id, $"{first} {last}".Trim(), img, department, position);

    private async Task<List<CoworkerCardModel>> ToCardsAsync(List<CardRow> rows, CancellationToken token)
    {
        var result = new List<CoworkerCardModel>(rows.Count);
        foreach (var r in rows) result.Add(await FillAsync(new CoworkerCardModel(), r, token));
        return result;
    }

    private async Task<T> FillAsync<T>(T model, CardRow row, CancellationToken token) where T : CoworkerCardModel
    {
        model.Id = row.Id;
        model.FullName = row.FullName;
        model.ProfileImg = await _imageUrls.DisplayUrlAsync(row.ProfileImg, token);
        model.DepartmentName = row.Department;
        model.PositionName = row.Position;
        return model;
    }
}
