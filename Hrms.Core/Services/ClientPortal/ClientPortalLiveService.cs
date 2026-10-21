using Hrms.Core.Services.Deployments;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MassTransit;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>
/// The live feed to the Client Portal (see <see cref="ClientPortalLiveBuilder"/>): punches right after they are saved, and the manning
/// planned for the coming days. Nothing is sent while the company's portal connection is off. Live data is a convenience for monitoring:
/// callers never fail a save because it could not be sent.
/// </summary>
public class ClientPortalLiveService : BaseService<Attendance>
{
    /// <summary>How many days of manning are sent at a time, starting today.</summary>
    public const int ManningDays = 7;

    private readonly ClientPortalSettingsService _settings;
    private readonly IPublishEndpoint _publisher;
    private readonly ITenantProvider _tenant;

    public ClientPortalLiveService(IUnitOfWorkService uow, ClientPortalSettingsService settings, IPublishEndpoint publisher, ITenantProvider tenant) : base(uow)
    {
        _settings = settings;
        _publisher = publisher;
        _tenant = tenant;
    }

    /// <summary>Sends saved punches that belong to a client and site and are recent enough to be live. Returns how many were sent.</summary>
    public async Task<int> PublishPunchesAsync(IReadOnlyList<Attendance> saved, CancellationToken token)
    {
        var candidates = saved.Where(p => p.EmployeeId is not null && p.ClientId is not null && p.OperationAreaId is not null).ToList();
        if (candidates.Count == 0 || !await _settings.IsEnabledAsync(token))
            return 0;

        var clientIds = candidates.Select(p => p.ClientId!.Value).Distinct().ToList();
        var areaIds = candidates.Select(p => p.OperationAreaId!.Value).Distinct().ToList();
        var employeeIds = candidates.Select(p => p.EmployeeId!.Value).Distinct().ToList();
        var from = DateOnly.FromDateTime(candidates.Min(p => p.WorkDateTime));
        var to = DateOnly.FromDateTime(candidates.Max(p => p.WorkDateTime));

        var clients = await Context.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, token);
        var areas = await Context.Areas.AsNoTracking().Where(a => areaIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, token);
        var employees = await Context.Employees.AsNoTracking().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, token);
        var sites = await Context.ClientSites.AsNoTracking().Where(s => clientIds.Contains(s.ClientId)).ToListAsync(token);
        var siteIds = sites.Select(s => s.Id).ToList();
        var posts = await Context.ClientPosts.AsNoTracking().Where(p => siteIds.Contains(p.ClientSiteId)).ToDictionaryAsync(p => p.Id, token);
        var postIds = posts.Keys.ToList();
        var deployments = await Context.Deployments.AsNoTracking()
            .Where(d => employeeIds.Contains(d.EmployeeId) && postIds.Contains(d.PostId) && d.StartDate <= to && (d.EndDate == null || d.EndDate >= from))
            .ToListAsync(token);

        var messages = ClientPortalLiveBuilder.Punches(new LivePunchSource(candidates, clients, areas, employees, sites, posts, deployments), _tenant.TenantId, LocalNow());
        foreach (var message in messages)
            await _publisher.Publish(message, token);
        return messages.Sum(m => m.Punches.Count);
    }

    /// <summary>Sends this company's manning for the coming week (one client, or all). Refused while the portal connection is off.</summary>
    public async Task<int> PublishManningAsync(Guid? clientId, CancellationToken token)
    {
        if (!await _settings.IsEnabledAsync(token))
            throw new ValidationException("Switch on the Client Portal connection first.");
        var today = DeploymentClock.Today();
        var source = await LoadManningAsync(Context, tenantId: null, clientId, today, today.AddDays(ManningDays - 1), token);
        var messages = ClientPortalLiveBuilder.Manning(source, _tenant.TenantId, today, today.AddDays(ManningDays - 1));
        foreach (var message in messages)
            await _publisher.Publish(message, token);
        return messages.Count;
    }

    /// <summary>
    /// The daily job's version: every company whose portal connection is on, each with its own data only (rows are read without the
    /// company filter, so each query names the company). Returns how many messages were sent.
    /// </summary>
    public static async Task<int> PublishManningForAllCompaniesAsync(HrmsContext db, IPublishEndpoint publisher, DateOnly today, CancellationToken token)
    {
        var tenants = (await db.GeneralSettings.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.DeletedAt == null && s.IdentityType == ClientPortalSettingsService.IdentityType && s.Description == ClientPortalSettingsService.EnabledKey)
                .Select(s => new { s.TenantId, s.Value })
                .ToListAsync(token))
            .Where(s => bool.TryParse(s.Value, out var on) && on)
            .Select(s => s.TenantId)
            .Distinct()
            .ToList();

        var sent = 0;
        var to = today.AddDays(ManningDays - 1);
        foreach (var tenantId in tenants)
        {
            var source = await LoadManningAsync(db, tenantId, clientId: null, today, to, token);
            foreach (var message in ClientPortalLiveBuilder.Manning(source, tenantId, today, to))
            {
                await publisher.Publish(message, token);
                sent++;
            }
        }
        return sent;
    }

    /// <summary>A company's manning data: with <paramref name="tenantId"/> null the context's own company filter applies, else that company's rows.</summary>
    private static async Task<ManningSource> LoadManningAsync(HrmsContext db, Guid? tenantId, Guid? clientId, DateOnly from, DateOnly to, CancellationToken token)
    {
        IQueryable<TEntity> Set<TEntity>() where TEntity : BaseEntity =>
            tenantId is { } t
                ? db.Set<TEntity>().IgnoreQueryFilters().Where(x => x.DeletedAt == null && x.TenantId == t).AsNoTracking()
                : db.Set<TEntity>().AsNoTracking();

        var sites = await Set<ClientSite>().Where(s => clientId == null || s.ClientId == clientId).ToListAsync(token);
        var clientIds = sites.Select(s => s.ClientId).Distinct().ToList();
        var siteIds = sites.Select(s => s.Id).ToList();
        var clients = await Set<Client>().Where(c => clientIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, token);
        var posts = await Set<ClientPost>().Where(p => siteIds.Contains(p.ClientSiteId)).ToDictionaryAsync(p => p.Id, token);
        var postIds = posts.Keys.ToList();
        var requirements = await Set<PostRequirement>()
            .Where(r => postIds.Contains(r.PostId) && r.EffectiveFrom <= to && (r.EffectiveTo == null || r.EffectiveTo >= from)).ToListAsync(token);
        var deployments = await Set<Deployment>()
            .Where(d => postIds.Contains(d.PostId) && d.StartDate <= to && (d.EndDate == null || d.EndDate >= from)).ToListAsync(token);
        var employeeIds = deployments.Select(d => d.EmployeeId).Distinct().ToList();
        var employees = await Set<Employee>().Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, token);
        var shiftIds = requirements.Where(r => r.TimeShiftId != null).Select(r => r.TimeShiftId!.Value).Distinct().ToList();
        var shifts = await Set<TimeShift>().Where(s => shiftIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => (s.StartTime, s.EndTime), token);

        return new ManningSource(clients, sites, posts, requirements, deployments, employees, shifts);
    }

    private static DateTime LocalNow() => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Manila), DateTimeKind.Unspecified);

    private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
}
