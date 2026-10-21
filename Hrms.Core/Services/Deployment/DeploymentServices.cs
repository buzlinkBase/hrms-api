using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.Deployments;

/// <summary>Dates as the company lives them (Philippine time), for "today" in deployment and manning rules.</summary>
public static class DeploymentClock
{
    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");

    public static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Manila));
}

/// <summary>Days of the week a requirement applies on, stored as a comma list in week order (empty = every day).</summary>
public static class WeekDays
{
    private static readonly string[] Order = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    public static string Normalize(IReadOnlyList<string>? days)
    {
        if (days is null || days.Count == 0) return string.Empty;
        var wanted = days.Select(d => d.Trim()).Where(d => d.Length > 0).ToList();
        var unknown = wanted.Where(d => !Order.Contains(d, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0)
            throw new ValidationException($"Unknown day(s): {string.Join(", ", unknown)}. Use Mon, Tue, Wed, Thu, Fri, Sat, Sun.");
        var picked = Order.Where(o => wanted.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
        return picked.Count == Order.Length ? string.Empty : string.Join(",", picked);
    }

    public static IReadOnlyList<string> Parse(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? [] : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static bool Includes(string? stored, DateOnly date) =>
        string.IsNullOrWhiteSpace(stored) || Parse(stored).Contains(Order[((int)date.DayOfWeek + 6) % 7], StringComparer.OrdinalIgnoreCase);
}

/// <summary>A client's sites: where it is served, each tied to a project site for attendance and payroll.</summary>
public class ClientSiteService : BaseService<ClientSite>
{
    public ClientSiteService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<List<ClientSiteModel>> ListAsync(Guid clientId, DateOnly today, CancellationToken token)
    {
        var sites = await GetQueryable(x => x.ClientId == clientId).OrderBy(x => x.Name).ToListAsync(token);
        var siteIds = sites.Select(s => s.Id).ToList();
        var areaIds = sites.Select(s => s.AreaId).Distinct().ToList();
        var areas = await Context.Areas.AsNoTracking().Where(a => areaIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, token);
        var posts = await Context.ClientPosts.AsNoTracking().Where(p => siteIds.Contains(p.ClientSiteId)).Select(p => new { p.Id, p.ClientSiteId }).ToListAsync(token);
        var postIds = posts.Select(p => p.Id).ToList();
        var requirements = await Context.PostRequirements.AsNoTracking().Where(r => postIds.Contains(r.PostId)).ToListAsync(token);
        var deployed = await Context.Deployments.AsNoTracking()
            .Where(d => postIds.Contains(d.PostId) && d.Role == DeploymentRole.Regular && d.StartDate <= today && (d.EndDate == null || d.EndDate >= today))
            .Select(d => d.PostId).ToListAsync(token);

        return sites.Select(s =>
        {
            var mine = posts.Where(p => p.ClientSiteId == s.Id).Select(p => p.Id).ToHashSet();
            var required = requirements.Where(r => mine.Contains(r.PostId) && PostRequirementRules.AppliesOn(r, today)).Sum(r => r.RequiredHeadcount);
            return new ClientSiteModel(s.Id, s.ClientId, s.Code, s.Name, s.AreaId, areas.GetValueOrDefault(s.AreaId, "—"), s.SubAreaLabel, s.StartDate, s.EndDate,
                mine.Count, required, deployed.Count(mine.Contains));
        }).ToList();
    }

    public async Task<ClientSite> SaveAsync(Guid clientId, Guid? id, ClientSiteRequest request, CancellationToken token)
    {
        if (!await Context.Clients.AnyAsync(c => c.Id == clientId, token))
            throw new NotFoundException("Client not found.");
        if (!await Context.Areas.AnyAsync(a => a.Id == request.AreaId, token))
            throw new NotFoundException("Project site not found.");
        var name = Required(request.Name, "site name", 200);
        var code = Required(request.Code, "site code", 40);
        if (request.EndDate is { } end && request.StartDate is { } start && end < start)
            throw new ValidationException("The end date is before the start date.");

        var others = await GetQueryable(x => x.ClientId == clientId && x.Id != (id ?? Guid.Empty)).ToListAsync(token);
        if (others.Any(o => o.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException($"This client already has a site named '{name}'.");
        if (others.Any(o => o.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException($"This client already has a site with code '{code}'.");

        ClientSite site;
        if (id is { } existingId)
        {
            site = await GetOneAsync(existingId, token) ?? throw new NotFoundException("Site not found.");
            if (site.ClientId != clientId) throw new NotFoundException("Site not found.");
        }
        else
        {
            site = new ClientSite { ClientId = clientId };
        }

        site.Code = code;
        site.Name = name;
        site.AreaId = request.AreaId;
        site.SubAreaLabel = Optional(request.SubAreaLabel, 100);
        site.StartDate = request.StartDate;
        site.EndDate = request.EndDate;
        if (id is null) await CreateAsync(site, token);
        else await ModifyAsync(site, token);
        await CommitChangesAsync(token);
        return site;
    }

    public async Task DeleteAsync(Guid id, DateOnly today, CancellationToken token)
    {
        var site = await GetOneAsync(id, token) ?? throw new NotFoundException("Site not found.");
        var postIds = await Context.ClientPosts.Where(p => p.ClientSiteId == id).Select(p => p.Id).ToListAsync(token);
        if (await Context.Deployments.AnyAsync(d => postIds.Contains(d.PostId) && (d.EndDate == null || d.EndDate >= today), token))
            throw new ValidationException("People are still deployed to this site's posts. End their deployments first.");
        await RemoveAsync(site);
        await CommitChangesAsync(token);
    }

    internal static string Required(string? value, string what, int max)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) throw new ValidationException($"Enter the {what}.");
        if (v.Length > max) throw new ValidationException($"The {what} can be at most {max} characters.");
        return v;
    }

    internal static string? Optional(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length <= max ? v : v[..max];
    }
}

/// <summary>When a requirement applies.</summary>
public static class PostRequirementRules
{
    public static bool AppliesOn(PostRequirement r, DateOnly date) =>
        r.EffectiveFrom <= date && (r.EffectiveTo == null || r.EffectiveTo >= date) && WeekDays.Includes(r.DaysOfWeek, date);
}

/// <summary>Posts at a client site and their contracted manning (requirements), which change only by adding a newer one.</summary>
public class ClientPostService : BaseService<ClientPost>
{
    public const int MaxHeadcount = 500;

    public ClientPostService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<List<ClientPostModel>> ListAsync(Guid siteId, DateOnly today, CancellationToken token)
    {
        var posts = await GetQueryable(x => x.ClientSiteId == siteId).OrderBy(x => x.Code).ToListAsync(token);
        var postIds = posts.Select(p => p.Id).ToList();
        var requirements = await Context.PostRequirements.AsNoTracking().Where(r => postIds.Contains(r.PostId))
            .OrderByDescending(r => r.EffectiveFrom).ToListAsync(token);
        var regulars = await Context.Deployments.AsNoTracking()
            .Where(d => postIds.Contains(d.PostId) && d.Role == DeploymentRole.Regular && d.StartDate <= today && (d.EndDate == null || d.EndDate >= today))
            .Select(d => d.PostId).ToListAsync(token);

        return posts.Select(p =>
        {
            var mine = requirements.Where(r => r.PostId == p.Id).ToList();
            var current = mine.Where(r => r.EffectiveFrom <= today && (r.EffectiveTo == null || r.EffectiveTo >= today)).OrderBy(r => r.ShiftLabel).Select(Map).ToList();
            return new ClientPostModel(p.Id, p.ClientSiteId, p.Code, p.Name, p.Position, p.Armed, p.Notes, current, mine.Select(Map).ToList(),
                regulars.Count(id => id == p.Id));
        }).ToList();
    }

    public async Task<ClientPost> SaveAsync(Guid siteId, Guid? id, ClientPostRequest request, CancellationToken token)
    {
        if (!await Context.ClientSites.AnyAsync(s => s.Id == siteId, token))
            throw new NotFoundException("Site not found.");
        var code = ClientSiteService.Required(request.Code, "post code", 40);
        var name = ClientSiteService.Required(request.Name, "post name", 200);
        var position = ClientSiteService.Required(request.Position, "position the post needs", 100);
        var taken = await GetQueryable(x => x.ClientSiteId == siteId && x.Id != (id ?? Guid.Empty)).Select(x => x.Code).ToListAsync(token);
        if (taken.Any(c => c.Trim().Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationException($"This site already has a post with code '{code}'.");

        ClientPost post;
        if (id is { } existingId)
        {
            post = await GetOneAsync(existingId, token) ?? throw new NotFoundException("Post not found.");
            if (post.ClientSiteId != siteId) throw new NotFoundException("Post not found.");
        }
        else
        {
            post = new ClientPost { ClientSiteId = siteId };
        }

        post.Code = code;
        post.Name = name;
        post.Position = position;
        post.Armed = request.Armed;
        post.Notes = ClientSiteService.Optional(request.Notes, 500);
        if (id is null) await CreateAsync(post, token);
        else await ModifyAsync(post, token);
        await CommitChangesAsync(token);
        return post;
    }

    public async Task DeleteAsync(Guid id, DateOnly today, CancellationToken token)
    {
        var post = await GetOneAsync(id, token) ?? throw new NotFoundException("Post not found.");
        if (await Context.Deployments.AnyAsync(d => d.PostId == id && (d.EndDate == null || d.EndDate >= today), token))
            throw new ValidationException("People are still deployed to this post. End their deployments first.");
        await RemoveAsync(post);
        await CommitChangesAsync(token);
    }

    /// <summary>
    /// Adds the manning of a post's shift from a date. The requirement in force for the same shift is closed the day before; one starting
    /// on the same day is replaced. A requirement may not start before the latest one of its shift.
    /// </summary>
    public async Task<PostRequirementModel> AddRequirementAsync(Guid postId, PostRequirementRequest request, CancellationToken token)
    {
        if (!await GetQueryable(x => x.Id == postId).AnyAsync(token))
            throw new NotFoundException("Post not found.");
        var shift = ClientSiteService.Required(request.ShiftLabel, "shift (for example DS or NS)", 60);
        if (request.RequiredHeadcount is < 1 or > MaxHeadcount)
            throw new ValidationException($"The headcount must be between 1 and {MaxHeadcount}.");
        if (request.EffectiveTo is { } to && to < request.EffectiveFrom)
            throw new ValidationException("The end date is before the start date.");
        if (request.TimeShiftId is { } timeShiftId && !await Context.TimeShifts.AnyAsync(t => t.Id == timeShiftId, token))
            throw new NotFoundException("Time shift not found.");
        var days = WeekDays.Normalize(request.DaysOfWeek);

        var sameShift = (await Context.PostRequirements.Where(r => r.PostId == postId).ToListAsync(token))
            .Where(r => r.ShiftLabel.Trim().Equals(shift, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameShift.Any(r => r.EffectiveFrom > request.EffectiveFrom))
            throw new ValidationException($"A later manning for shift {shift} already exists. Add changes from the latest date onward.");

        foreach (var previous in sameShift.Where(r => r.EffectiveTo == null || r.EffectiveTo >= request.EffectiveFrom))
        {
            if (previous.EffectiveFrom == request.EffectiveFrom)
                Context.PostRequirements.Remove(previous);
            else
                previous.EffectiveTo = request.EffectiveFrom.AddDays(-1);
        }

        var requirement = new PostRequirement
        {
            PostId = postId, ShiftLabel = shift, TimeShiftId = request.TimeShiftId, StartTime = request.StartTime, EndTime = request.EndTime,
            RequiredHeadcount = request.RequiredHeadcount, DaysOfWeek = days, EffectiveFrom = request.EffectiveFrom, EffectiveTo = request.EffectiveTo,
        };
        await Context.PostRequirements.AddAsync(requirement, token);
        await CommitChangesAsync(token);
        return Map(requirement);
    }

    /// <summary>Stops a requirement from a date (the post no longer needs that shift).</summary>
    public async Task EndRequirementAsync(Guid requirementId, DateOnly lastDay, CancellationToken token)
    {
        var requirement = await Context.PostRequirements.FirstOrDefaultAsync(r => r.Id == requirementId, token)
                          ?? throw new NotFoundException("Requirement not found.");
        if (lastDay < requirement.EffectiveFrom)
            throw new ValidationException("The last day is before the requirement starts.");
        requirement.EffectiveTo = lastDay;
        await CommitChangesAsync(token);
    }

    public static PostRequirementModel Map(PostRequirement r) =>
        new(r.Id, r.PostId, r.ShiftLabel, r.TimeShiftId, r.StartTime, r.EndTime, r.RequiredHeadcount, WeekDays.Parse(r.DaysOfWeek), r.EffectiveFrom, r.EffectiveTo);
}

/// <summary>
/// Who is deployed where. A person has one regular deployment at a time: assigning a new one ends the old one the day before. Relievers
/// and augmentation run alongside it. When a regular deployment is in force, the employee's client and project site follow it.
/// </summary>
public class DeploymentService : BaseService<Deployment>
{
    public DeploymentService(IUnitOfWorkService uow) : base(uow)
    {
    }

    public async Task<List<DeploymentModel>> ListAsync(DeploymentQuery query, DateOnly today, CancellationToken token)
    {
        var posts = Context.ClientPosts.AsNoTracking();
        if (query.SiteId is { } siteId) posts = posts.Where(p => p.ClientSiteId == siteId);
        if (query.ClientId is { } clientId)
        {
            var siteIds = Context.ClientSites.Where(s => s.ClientId == clientId).Select(s => s.Id);
            posts = posts.Where(p => siteIds.Contains(p.ClientSiteId));
        }
        var postIds = posts.Select(p => p.Id);

        var q = GetQueryable();
        if (query.ClientId is not null || query.SiteId is not null) q = q.Where(d => postIds.Contains(d.PostId));
        if (query.PostId is { } postId) q = q.Where(d => d.PostId == postId);
        if (query.EmployeeId is { } employeeId) q = q.Where(d => d.EmployeeId == employeeId);
        if (query.ActiveOn is { } on) q = q.Where(d => d.StartDate <= on && (d.EndDate == null || d.EndDate >= on));
        var rows = await q.OrderByDescending(d => d.StartDate).Take(2000).ToListAsync(token);
        return await MapAsync(rows, today, token);
    }

    public async Task<DeploymentModel> AssignAsync(AssignDeploymentRequest request, DateOnly today, CancellationToken token)
    {
        var employee = await Context.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, token)
                       ?? throw new NotFoundException("Employee not found.");
        var post = await Context.ClientPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PostId, token)
                   ?? throw new NotFoundException("Post not found.");
        var site = await Context.ClientSites.AsNoTracking().FirstAsync(s => s.Id == post.ClientSiteId, token);
        if (request.EndDate is { } end && end < request.StartDate)
            throw new ValidationException("The end date is before the start date.");
        if (request.Role == DeploymentRole.Augmentation && request.EndDate is null)
            throw new ValidationException("Augmentation is temporary: enter its last day.");

        var own = await GetQueryable(d => d.EmployeeId == request.EmployeeId, noTracking: false).ToListAsync(token);
        if (own.Any(d => d.PostId == request.PostId && d.Role == request.Role && Overlaps(d, request.StartDate, request.EndDate)))
            throw new ValidationException("This person already has that deployment for part of these dates.");

        if (request.Role == DeploymentRole.Regular)
        {
            // A transfer: the regular deployment in force ends the day before the new one starts.
            foreach (var current in own.Where(d => d.Role == DeploymentRole.Regular && Overlaps(d, request.StartDate, request.EndDate)))
            {
                if (current.StartDate >= request.StartDate)
                    throw new ValidationException($"This person has a regular deployment starting {current.StartDate:MMM d, yyyy}. Change that one instead.");
                current.EndDate = request.StartDate.AddDays(-1);
                current.Reason ??= "Transferred";
            }
        }

        var deployment = new Deployment
        {
            EmployeeId = request.EmployeeId, PostId = request.PostId, Role = request.Role, StartDate = request.StartDate, EndDate = request.EndDate,
            Reason = ClientSiteService.Optional(request.Reason, 500), EventTag = ClientSiteService.Optional(request.EventTag, 200),
        };
        await CreateAsync(deployment, token);

        if (request.Role == DeploymentRole.Regular && request.StartDate <= today && (request.EndDate is null || request.EndDate >= today))
        {
            employee.ClientId = site.ClientId;
            employee.AreaId = site.AreaId;
        }
        await CommitChangesAsync(token);
        return (await MapAsync([deployment], today, token))[0];
    }

    public async Task<DeploymentModel> EndAsync(Guid id, EndDeploymentRequest request, DateOnly today, CancellationToken token)
    {
        var deployment = await GetOneAsync(id, token) ?? throw new NotFoundException("Deployment not found.");
        if (request.EndDate < deployment.StartDate)
            throw new ValidationException("The last day is before the deployment starts.");
        deployment.EndDate = request.EndDate;
        deployment.Reason = ClientSiteService.Optional(request.Reason, 500) ?? deployment.Reason;
        await CommitChangesAsync(token);
        return (await MapAsync([deployment], today, token))[0];
    }

    /// <summary>
    /// Brings employees' client and project site in line with the regular deployment in force today (for deployments that started after
    /// they were entered). Meant for a daily job; returns how many employees changed.
    /// </summary>
    public async Task<int> SyncCurrentAsync(DateOnly today, CancellationToken token)
    {
        var changed = await SyncAsync(Context, today, everyCompany: false, token);
        if (changed > 0) await CommitChangesAsync(token);
        return changed;
    }

    /// <summary>The daily job's version: every company at once (no company filter), skipping deleted rows itself. The caller saves.</summary>
    public static Task<int> SyncAllCompaniesAsync(HrmsContext db, DateOnly today, CancellationToken token) =>
        SyncAsync(db, today, everyCompany: true, token);

    private static async Task<int> SyncAsync(HrmsContext db, DateOnly today, bool everyCompany, CancellationToken token)
    {
        IQueryable<TEntity> Set<TEntity>() where TEntity : BaseEntity =>
            everyCompany ? db.Set<TEntity>().IgnoreQueryFilters().Where(x => x.DeletedAt == null) : db.Set<TEntity>();

        var current = await Set<Deployment>().AsNoTracking()
            .Where(d => d.Role == DeploymentRole.Regular && d.StartDate <= today && (d.EndDate == null || d.EndDate >= today)).ToListAsync(token);
        if (current.Count == 0) return 0;
        var postIds = current.Select(d => d.PostId).Distinct().ToList();
        var posts = await Set<ClientPost>().AsNoTracking().Where(p => postIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ClientSiteId, token);
        var siteIds = posts.Values.Distinct().ToList();
        var sites = await Set<ClientSite>().AsNoTracking().Where(s => siteIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, token);
        var employeeIds = current.Select(d => d.EmployeeId).Distinct().ToList();
        var employees = await Set<Employee>().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, token);

        var changed = 0;
        foreach (var d in current)
        {
            if (!employees.TryGetValue(d.EmployeeId, out var employee) || !posts.TryGetValue(d.PostId, out var siteId) || !sites.TryGetValue(siteId, out var site))
                continue;
            // Never across companies, whatever the ids say.
            if (employee.TenantId != site.TenantId) continue;
            if (employee.ClientId == site.ClientId && employee.AreaId == site.AreaId) continue;
            employee.ClientId = site.ClientId;
            employee.AreaId = site.AreaId;
            changed++;
        }
        return changed;
    }

    /// <summary>Manning of a client's posts on a day: people required by the requirements in force against people deployed.</summary>
    public async Task<CoverageReport> CoverageAsync(Guid clientId, DateOnly date, CancellationToken token)
    {
        var sites = await Context.ClientSites.AsNoTracking().Where(s => s.ClientId == clientId).ToDictionaryAsync(s => s.Id, token);
        var siteIds = sites.Keys.ToList();
        var posts = await Context.ClientPosts.AsNoTracking().Where(p => siteIds.Contains(p.ClientSiteId)).ToListAsync(token);
        var postIds = posts.Select(p => p.Id).ToList();
        var requirements = (await Context.PostRequirements.AsNoTracking().Where(r => postIds.Contains(r.PostId)).ToListAsync(token))
            .Where(r => PostRequirementRules.AppliesOn(r, date)).ToList();
        var deployments = await GetQueryable(d => postIds.Contains(d.PostId) && d.StartDate <= date && (d.EndDate == null || d.EndDate >= date)).ToListAsync(token);

        var rows = posts.Select(p =>
            {
                var needs = requirements.Where(r => r.PostId == p.Id).OrderBy(r => r.ShiftLabel).ToList();
                var mine = deployments.Where(d => d.PostId == p.Id).ToList();
                var required = needs.Sum(r => r.RequiredHeadcount);
                var regulars = mine.Count(d => d.Role == DeploymentRole.Regular);
                var relievers = mine.Count(d => d.Role == DeploymentRole.Reliever);
                var site = sites[p.ClientSiteId];
                return new CoverageRow(site.Id, site.Name, p.Id, p.Name, p.Position, string.Join(" · ", needs.Select(r => $"{r.ShiftLabel} {r.RequiredHeadcount}")),
                    required, regulars, relievers, mine.Count(d => d.Role == DeploymentRole.Augmentation), required - regulars - relievers);
            })
            .Where(r => r.Required > 0 || r.Regulars + r.Relievers + r.Augmentation > 0)
            .OrderBy(r => r.SiteName).ThenBy(r => r.PostName).ToList();

        var totalRequired = rows.Sum(r => r.Required);
        var deployed = rows.Sum(r => r.Regulars + r.Relievers);
        return new CoverageReport(clientId, date, totalRequired, deployed, rows.Sum(r => Math.Max(0, r.Gap)), rows);
    }

    private static bool Overlaps(Deployment d, DateOnly start, DateOnly? end) =>
        d.StartDate <= (end ?? DateOnly.MaxValue) && (d.EndDate ?? DateOnly.MaxValue) >= start;

    private async Task<List<DeploymentModel>> MapAsync(IReadOnlyList<Deployment> rows, DateOnly today, CancellationToken token)
    {
        var employeeIds = rows.Select(r => r.EmployeeId).Distinct().ToList();
        var employees = await Context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id))
            .Select(e => new { e.Id, e.EmployeeNo, e.FirstName, e.LastName }).ToDictionaryAsync(e => e.Id, token);
        var postIds = rows.Select(r => r.PostId).Distinct().ToList();
        var posts = await Context.ClientPosts.AsNoTracking().Where(p => postIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, token);
        var siteIds = posts.Values.Select(p => p.ClientSiteId).Distinct().ToList();
        var sites = await Context.ClientSites.AsNoTracking().Where(s => siteIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, token);
        var clientIds = sites.Values.Select(s => s.ClientId).Distinct().ToList();
        var clients = await Context.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, token);

        return rows.Select(d =>
        {
            var e = employees.GetValueOrDefault(d.EmployeeId);
            var p = posts.GetValueOrDefault(d.PostId);
            var s = p is null ? null : sites.GetValueOrDefault(p.ClientSiteId);
            return new DeploymentModel(d.Id, d.EmployeeId, e?.EmployeeNo ?? "—", e is null ? "—" : $"{e.LastName}, {e.FirstName}", d.PostId, p?.Name ?? "—",
                s?.Id ?? Guid.Empty, s?.Name ?? "—", s?.ClientId ?? Guid.Empty, s is null ? "—" : clients.GetValueOrDefault(s.ClientId, "—"), d.Role, d.StartDate,
                d.EndDate, d.Reason, d.EventTag, d.StartDate <= today && (d.EndDate == null || d.EndDate >= today));
        }).ToList();
    }
}
