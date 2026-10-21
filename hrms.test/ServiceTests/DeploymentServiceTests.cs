using Hrms.Core;
using Hrms.Core.Services;
using Hrms.Core.Services.Deployments;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace hrms.test.ServiceTests;

/// <summary>Client sites, posts, contracted manning and deployment history (Setup > Deployment).</summary>
public class DeploymentServiceTests : IDisposable
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    private readonly SqliteHrmsContext _db = new();
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly Guid _otherClientId = Guid.NewGuid();
    private readonly Guid _areaId = Guid.NewGuid();
    private readonly Guid _otherAreaId = Guid.NewGuid();
    private readonly Guid _employeeId = Guid.NewGuid();
    private readonly Guid _payrollGroupId = Guid.NewGuid();

    public DeploymentServiceTests()
    {
        using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = _payrollGroupId });
        seed.Clients.AddRange(new Client { Id = _clientId, Code = "WCC", Name = "Waterfront" }, new Client { Id = _otherClientId, Code = "SM", Name = "SM Prime" });
        seed.Areas.AddRange(new CostCenters { Id = _areaId, Code = "LAHUG", Name = "Lahug" }, new CostCenters { Id = _otherAreaId, Code = "CEBU", Name = "Cebu City" });
        seed.Employees.Add(new Employee { Id = _employeeId, EmployeeNo = "E-1", FirstName = "Romnick", LastName = "Barulo", PayrollGroupId = _payrollGroupId });
        seed.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    /// <summary>Each step in its own context and unit of work, as each request is in the API.</summary>
    private async Task<T> Run<T>(Func<IUnitOfWorkService, Task<T>> step)
    {
        await using var context = _db.NewContext();
        return await step(new UnitOfWorkService(context));
    }

    private Task<ClientSite> SiteAsync(Guid? clientId = null, Guid? areaId = null, string name = "Main", string code = "MAIN") =>
        Run(uow => new ClientSiteService(uow).SaveAsync(clientId ?? _clientId, null, new ClientSiteRequest(code, name, areaId ?? _areaId, null, null, null), CancellationToken.None));

    private Task<ClientPost> PostAsync(Guid siteId, string code = "GATE") =>
        Run(uow => new ClientPostService(uow).SaveAsync(siteId, null, new ClientPostRequest(code, $"Post {code}", "Regular guard", true, null), CancellationToken.None));

    private Task<PostRequirementModel> RequireAsync(Guid postId, string shift, int headcount, DateOnly from, params string[] days) =>
        Run(uow => new ClientPostService(uow).AddRequirementAsync(postId, new PostRequirementRequest(shift, null, null, null, headcount, days, from, null), CancellationToken.None));

    private Task<DeploymentModel> AssignAsync(Guid postId, DeploymentRole role, DateOnly start, DateOnly? end = null, DateOnly? today = null, Guid? employeeId = null) =>
        Run(uow => new DeploymentService(uow).AssignAsync(new AssignDeploymentRequest(employeeId ?? _employeeId, postId, role, start, end, null, null), today ?? start,
            CancellationToken.None));

    private async Task<Employee> EmployeeAsync()
    {
        await using var context = _db.NewContext();
        return await context.Employees.AsNoTracking().FirstAsync(e => e.Id == _employeeId);
    }

    [Fact]
    public async Task Sites_are_unique_by_name_and_code_within_a_client_and_need_a_real_project_site()
    {
        await SiteAsync();

        var sameName = () => SiteAsync(name: "main ", code: "OTHER");
        await sameName.Should().ThrowAsync<ValidationException>().WithMessage("*named*");
        var sameCode = () => SiteAsync(name: "Other", code: "main");
        await sameCode.Should().ThrowAsync<ValidationException>().WithMessage("*code*");
        await FluentActions.Awaiting(() => SiteAsync(clientId: _otherClientId)).Should().NotThrowAsync("another client may use the same name");
        var noArea = () => SiteAsync(name: "Ghost", code: "GHOST", areaId: Guid.NewGuid());
        await noArea.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Post_codes_are_unique_within_a_site()
    {
        var site = await SiteAsync();
        await PostAsync(site.Id);

        var again = () => PostAsync(site.Id, "gate");
        await again.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task A_new_manning_closes_the_previous_one_of_its_shift_and_one_on_the_same_day_replaces_it()
    {
        var site = await SiteAsync();
        var post = await PostAsync(site.Id);
        await RequireAsync(post.Id, "DS", 2, Monday);
        await RequireAsync(post.Id, "NS", 1, Monday);

        await RequireAsync(post.Id, "DS", 3, Monday.AddDays(10));
        await RequireAsync(post.Id, "DS", 4, Monday.AddDays(10));

        var listed = await Run(uow => new ClientPostService(uow).ListAsync(site.Id, Monday.AddDays(12), CancellationToken.None));
        var requirements = listed.Single().Requirements;
        requirements.Should().HaveCount(3, "the same-day DS change replaced the one before it");
        requirements.Single(r => r.ShiftLabel == "DS" && r.EffectiveFrom == Monday).EffectiveTo.Should().Be(Monday.AddDays(9));
        listed.Single().CurrentRequirements.Select(r => $"{r.ShiftLabel} {r.RequiredHeadcount}").Should().Equal("DS 4", "NS 1");

        var backdated = () => RequireAsync(post.Id, "DS", 1, Monday.AddDays(2));
        await backdated.Should().ThrowAsync<ValidationException>().WithMessage("*later manning*");
    }

    [Fact]
    public async Task Days_of_the_week_are_normalised_and_checked()
    {
        WeekDays.Normalize(["sun", "Mon"]).Should().Be("Mon,Sun");
        WeekDays.Normalize(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]).Should().BeEmpty("every day is stored as no restriction");
        WeekDays.Includes("Sat,Sun", Monday).Should().BeFalse();
        WeekDays.Includes("Sat,Sun", Monday.AddDays(6)).Should().BeTrue();
        FluentActions.Invoking(() => WeekDays.Normalize(["Funday"])).Should().Throw<ValidationException>();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task A_current_regular_deployment_sets_the_employees_client_and_site_and_a_new_one_is_a_transfer()
    {
        var waterfront = await SiteAsync();
        var sm = await SiteAsync(clientId: _otherClientId, areaId: _otherAreaId);
        var gate = await PostAsync(waterfront.Id);
        var lobby = await PostAsync(sm.Id, "LOBBY");

        await AssignAsync(gate.Id, DeploymentRole.Regular, Monday);
        (await EmployeeAsync()).ClientId.Should().Be(_clientId);

        await AssignAsync(lobby.Id, DeploymentRole.Regular, Monday.AddDays(7));

        var history = await Run(uow => new DeploymentService(uow).ListAsync(new DeploymentQuery(null, null, null, _employeeId, null), Monday.AddDays(7), CancellationToken.None));
        history.Single(d => d.PostId == gate.Id).EndDate.Should().Be(Monday.AddDays(6));
        history.Single(d => d.PostId == lobby.Id).Active.Should().BeTrue();
        var employee = await EmployeeAsync();
        (employee.ClientId, employee.AreaId).Should().Be((_otherClientId, _otherAreaId));
    }

    [Fact]
    public async Task A_future_regular_deployment_takes_effect_on_the_employee_when_its_day_comes()
    {
        var site = await SiteAsync();
        var post = await PostAsync(site.Id);

        await AssignAsync(post.Id, DeploymentRole.Regular, Monday.AddDays(5), today: Monday);
        (await EmployeeAsync()).ClientId.Should().BeNull("it has not started yet");

        (await Run(uow => new DeploymentService(uow).SyncCurrentAsync(Monday.AddDays(5), CancellationToken.None))).Should().Be(1);
        (await EmployeeAsync()).ClientId.Should().Be(_clientId);
        (await Run(uow => new DeploymentService(uow).SyncCurrentAsync(Monday.AddDays(6), CancellationToken.None))).Should().Be(0);
    }

    [Fact]
    public async Task The_daily_job_moves_employees_of_every_company_but_never_across_companies()
    {
        var site = await SiteAsync();
        var post = await PostAsync(site.Id);
        await AssignAsync(post.Id, DeploymentRole.Regular, Monday.AddDays(5), today: Monday);
        var otherCompany = Guid.NewGuid();
        await using (var seed = _db.NewContext())
        {
            // A deployment whose site belongs to another company: the job must leave the employee alone.
            var foreignSite = new ClientSite { Id = Guid.NewGuid(), TenantId = otherCompany, ClientId = _otherClientId, AreaId = _otherAreaId, Code = "X", Name = "X" };
            var foreignPost = new ClientPost { Id = Guid.NewGuid(), TenantId = otherCompany, ClientSiteId = foreignSite.Id, Code = "X", Name = "X", Position = "Guard" };
            seed.ClientSites.Add(foreignSite);
            seed.ClientPosts.Add(foreignPost);
            seed.Deployments.Add(new Deployment { EmployeeId = await NewEmployeeAsync("E-9"), PostId = foreignPost.Id, Role = DeploymentRole.Regular, StartDate = Monday });
            await seed.SaveChangesAsync();
        }

        await using (var job = _db.NewContext())
        {
            (await DeploymentService.SyncAllCompaniesAsync(job, Monday.AddDays(5), CancellationToken.None)).Should().Be(1);
            await job.SaveChangesAsync();
        }

        (await EmployeeAsync()).ClientId.Should().Be(_clientId);
    }

    [Fact]
    public async Task Relievers_do_not_move_the_employee_and_augmentation_needs_a_last_day()
    {
        var site = await SiteAsync();
        var post = await PostAsync(site.Id);

        await AssignAsync(post.Id, DeploymentRole.Reliever, Monday);
        (await EmployeeAsync()).ClientId.Should().BeNull();

        var open = () => AssignAsync(post.Id, DeploymentRole.Augmentation, Monday);
        await open.Should().ThrowAsync<ValidationException>().WithMessage("*last day*");
        var twice = () => AssignAsync(post.Id, DeploymentRole.Reliever, Monday.AddDays(3));
        await twice.Should().ThrowAsync<ValidationException>().WithMessage("*already*");
    }

    [Fact]
    public async Task Coverage_counts_required_against_deployed_per_post_on_the_day()
    {
        var site = await SiteAsync();
        var gate = await PostAsync(site.Id);
        var roving = await PostAsync(site.Id, "ROVE");
        await RequireAsync(gate.Id, "DS", 2, Monday);
        await RequireAsync(gate.Id, "NS", 1, Monday);
        await RequireAsync(roving.Id, "DS", 1, Monday, "Sat", "Sun");
        await AssignAsync(gate.Id, DeploymentRole.Regular, Monday);
        await AssignAsync(gate.Id, DeploymentRole.Augmentation, Monday, Monday.AddDays(1), employeeId: await NewEmployeeAsync("E-2"));

        var coverage = await Run(uow => new DeploymentService(uow).CoverageAsync(_clientId, Monday, CancellationToken.None));

        var row = coverage.Rows.Single();
        (row.Shifts, row.Required, row.Regulars, row.Augmentation, row.Gap).Should().Be(("DS 2 · NS 1", 3, 1, 1, 2));
        (coverage.Required, coverage.Deployed, coverage.Gap).Should().Be((3, 1, 2));

        var saturday = await Run(uow => new DeploymentService(uow).CoverageAsync(_clientId, Monday.AddDays(5), CancellationToken.None));
        saturday.Rows.Should().Contain(r => r.PostId == roving.Id && r.Required == 1 && r.Gap == 1);
    }

    [Fact]
    public async Task A_site_or_post_with_people_still_deployed_cannot_be_deleted()
    {
        var site = await SiteAsync();
        var post = await PostAsync(site.Id);
        var deployment = await AssignAsync(post.Id, DeploymentRole.Regular, Monday);

        var deleteSite = () => Run(async uow => { await new ClientSiteService(uow).DeleteAsync(site.Id, Monday, CancellationToken.None); return 0; });
        await deleteSite.Should().ThrowAsync<ValidationException>();
        var deletePost = () => Run(async uow => { await new ClientPostService(uow).DeleteAsync(post.Id, Monday, CancellationToken.None); return 0; });
        await deletePost.Should().ThrowAsync<ValidationException>();

        await Run(uow => new DeploymentService(uow).EndAsync(deployment.Id, new EndDeploymentRequest(Monday.AddDays(2), "Contract ended"), Monday, CancellationToken.None));
        await FluentActions.Awaiting(() => Run(async uow => { await new ClientPostService(uow).DeleteAsync(post.Id, Monday.AddDays(3), CancellationToken.None); return 0; }))
            .Should().NotThrowAsync();
    }

    private async Task<Guid> NewEmployeeAsync(string number)
    {
        var id = Guid.NewGuid();
        await using var context = _db.NewContext();
        context.Employees.Add(new Employee { Id = id, EmployeeNo = number, FirstName = "Carl Jay", LastName = "Bonute", PayrollGroupId = _payrollGroupId });
        await context.SaveChangesAsync();
        return id;
    }
}
