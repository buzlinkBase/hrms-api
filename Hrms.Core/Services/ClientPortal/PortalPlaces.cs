using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services.ClientPortal;

/// <summary>The portal's site and post for a client and project site, with the deployment that put the person there (if any).</summary>
public sealed record PortalPlace(
    Guid ClientId, Guid AreaId, string SiteExternalId, string SiteCode, string SiteName, string PostExternalId, string PostCode, string PostName,
    string? Position, Deployment? Deployment)
{
    /// <summary>True for a real post from Setup &gt; Deployment; false for the default post a site gets without one.</summary>
    public bool IsRealPost => Deployment is not null;
}

/// <summary>
/// Where the portal files a person's day or punch, shared by the DTR feed and the live feed so both land on the same site and post:
/// <list type="bullet">
/// <item>The site is the company's client site for the client and project site (Setup &gt; Deployment), else the project site itself.</item>
/// <item>The post is the one the person was deployed to there that day (a reliever or augmentation deployment wins over the regular one),
/// else a default post per site.</item>
/// </list>
/// </summary>
public sealed class PortalPlaces
{
    public const string DefaultPostCode = "MAIN";

    private readonly IReadOnlyDictionary<Guid, CostCenters> _areas;
    private readonly IReadOnlyList<ClientSite> _sites;
    private readonly IReadOnlyDictionary<Guid, ClientPost> _posts;
    private readonly ILookup<Guid, Deployment> _deploymentsByEmployee;

    public PortalPlaces(
        IReadOnlyDictionary<Guid, CostCenters> areas, IReadOnlyList<ClientSite>? sites, IReadOnlyDictionary<Guid, ClientPost>? posts, IReadOnlyList<Deployment>? deployments)
    {
        _areas = areas;
        _sites = sites ?? [];
        _posts = posts ?? new Dictionary<Guid, ClientPost>();
        _deploymentsByEmployee = (deployments ?? []).ToLookup(d => d.EmployeeId);
    }

    public PortalPlace Resolve(Guid employeeId, DateOnly date, Guid client, Guid area)
    {
        var deployment = DeploymentOn(employeeId, date, client, area);
        var site = deployment is not null && _posts.TryGetValue(deployment.PostId, out var deployedPost)
            ? _sites.FirstOrDefault(s => s.Id == deployedPost.ClientSiteId)
            : _sites.Where(s => s.ClientId == client && s.AreaId == area).OrderBy(s => s.Code).FirstOrDefault();

        _areas.TryGetValue(area, out var projectSite);
        var siteExt = site is not null ? Ext(site.Id) : Ext(area);
        var siteCode = site is not null ? Code(site.Code, site.Id) : Code(projectSite?.Code, area);
        var siteName = Name(site?.Name ?? projectSite?.Name, "Site");

        if (deployment is not null && _posts.TryGetValue(deployment.PostId, out var post))
            return new PortalPlace(client, area, siteExt, siteCode, siteName, Ext(post.Id), Code(post.Code, post.Id), Name(post.Name, "Post"), post.Position, deployment);

        return new PortalPlace(client, area, siteExt, siteCode, siteName, DefaultPostExt(siteExt), DefaultPostCode, siteName, null, null);
    }

    /// <summary>The site row of a client site, as the portal knows it.</summary>
    public static PortalSiteRow SiteRow(ClientSite site) => new(Ext(site.Id), Ext(site.ClientId), Code(site.Code, site.Id), Name(site.Name, "Site"));

    public static PortalPostRow PostRow(ClientPost post) => new(Ext(post.Id), Ext(post.ClientSiteId), Code(post.Code, post.Id), Name(post.Name, "Post"), post.Position);

    public static PortalClientRow ClientRow(Client client) => new(Ext(client.Id), Code(client.Code, client.Id), Name(client.Name, "Client"));

    public static PortalGuardRow GuardRow(Guid employeeId, Guid clientId, Employee? employee, string? fullName = null)
    {
        var name = !string.IsNullOrWhiteSpace(fullName) ? fullName.Trim()
            : employee is null ? "Guard" : $"{employee.LastName}, {employee.FirstName}".Trim(' ', ',');
        return new PortalGuardRow(Ext(employeeId), Ext(clientId), Code(employee?.EmployeeNo, employeeId), name);
    }

    private Deployment? DeploymentOn(Guid employeeId, DateOnly date, Guid client, Guid area) =>
        _deploymentsByEmployee[employeeId]
            .Where(d => d.StartDate <= date && (d.EndDate == null || d.EndDate >= date))
            .Where(d => _posts.TryGetValue(d.PostId, out var p) && _sites.Any(s => s.Id == p.ClientSiteId && s.ClientId == client && s.AreaId == area))
            // A reliever or augmentation deployment at this place says more about the day than the person's home post.
            .OrderBy(d => d.Role == DeploymentRole.Regular ? 1 : 0).ThenByDescending(d => d.StartDate)
            .FirstOrDefault();

    public static string Ext(Guid id) => id.ToString("D");

    public static string DefaultPostExt(string siteExternalId) => $"post-{siteExternalId}";

    public static string Name(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    // The portal requires a code; fall back to a short slice of the id when HRIS has none.
    public static string Code(string? value, Guid id) => string.IsNullOrWhiteSpace(value) ? id.ToString("N")[..8].ToUpperInvariant() : value.Trim();
}
