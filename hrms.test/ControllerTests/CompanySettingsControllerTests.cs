using System.Net;
using Hrms.Api.Controllers;
using Hrms.Core.Interfaces;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;

namespace hrms.test.ControllerTests;

/// <summary>
/// Company Info's "Company Name" is also the company's (tenant's) name everywhere outside HRIS
/// -- company switcher, access token, invitations -- so saving a new name must rename it in
/// Tenant Service too (which fans it out to Auth Service). The rename runs first: if Tenant
/// Service refuses, nothing is saved, so HRIS never shows a name the rest of the system doesn't.
/// </summary>
public class CompanySettingsControllerTests
{
    private static async Task<SqliteHrmsContext> SeedCompanyAsync(string name)
    {
        var db = new SqliteHrmsContext();
        await using var seed = db.NewContext();
        seed.Companies.Add(new Company { Id = Guid.NewGuid(), Code = "MAIN", Description = name });
        await seed.SaveChangesAsync();
        return db;
    }

    private static async Task<string?> SavedNameAsync(SqliteHrmsContext db)
    {
        await using var verify = db.NewContext();
        return (await verify.Companies.SingleAsync()).Description;
    }

    private static async Task PutAsync(SqliteHrmsContext db, ITenantClient tenantClient, string name)
    {
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!); // Put never touches the logo
        await controller.Put(new CreateCompany { Description = name }, CancellationToken.None);
    }

    [Fact]
    public async Task Put_NewName_RenamesTheTenant_ThenSaves()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();

        await PutAsync(db, tenantClient, "  Acme Corp ");

        await tenantClient.Received(1).RenameCurrentAsync("Acme Corp", Arg.Any<CancellationToken>());
        (await SavedNameAsync(db)).Should().Be("  Acme Corp ");
    }

    [Fact]
    public async Task Put_SameName_DoesNotCallTenantService()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();

        await PutAsync(db, tenantClient, "My Space");

        await tenantClient.DidNotReceive().RenameCurrentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_TenantServiceRefuses_SavesNothing()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        var refusal = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Put, "http://tenant-api/api/v1/tenants/current/name"),
            HttpMethod.Put,
            new HttpResponseMessage(HttpStatusCode.Forbidden),
            new RefitSettings());
        tenantClient.RenameCurrentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(refusal);

        var act = () => PutAsync(db, tenantClient, "Acme Corp");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nothing was saved*");
        (await SavedNameAsync(db)).Should().Be("My Space");
    }

    [Fact]
    public async Task Put_BlankName_DoesNotRenameTheTenant()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();

        try { await PutAsync(db, tenantClient, "   "); } catch { /* the company validator may reject it */ }

        await tenantClient.DidNotReceive().RenameCurrentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
