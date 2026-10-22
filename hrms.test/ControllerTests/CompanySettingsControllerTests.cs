using System.Net;
using Hrms.Api.Controllers;
using Hrms.Core.Interfaces;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Onepunch.Common.Lib.DTO;
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
    public async Task Put_ChangedAddressOrContact_SyncsTheConsoleCopy()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        await controller.Put(
            new CreateCompany { Description = "My Space", Address = "1 Main St", Contact = "555" },
            CancellationToken.None);

        await tenantClient.Received(1).UpdateCurrentProfileAsync(
            Arg.Is<TenantProfileUpdate>(p => p.Address == "1 Main St" && p.Contact == "555"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_UnchangedAddressAndContact_DoesNotSyncTheConsoleCopy()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();

        await PutAsync(db, tenantClient, "My Space");

        await tenantClient.DidNotReceive().UpdateCurrentProfileAsync(Arg.Any<TenantProfileUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedDefaultAsync_WithProfile_SeedsEveryEnteredField()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var service = new CompanyService(new UnitOfWorkService(context));

        await service.SeedDefaultAsync("Acme", new CompanyProfile
        {
            Address = "1 Main St",
            Contact = "555",
            Email = "hr@acme.test",
            Tin = "000-111",
            RdoCode = "044",
            SssNumber = "11",
            PhilHealthNumber = "22",
            PagIbigNumber = "33",
            AuthorizedSignatoryName = "Jane",
            AuthorizedSignatoryTitle = "HR Head",
        }, CancellationToken.None);
        await service.CommitChangesAsync();

        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.Description.Should().Be("Acme");
        company.Address.Should().Be("1 Main St");
        company.Contact.Should().Be("555");
        company.Email.Should().Be("hr@acme.test");
        company.TIN.Should().Be("000-111");
        company.RDOCode.Should().Be("044");
        company.SSSNumber.Should().Be("11");
        company.PhilHealthNumber.Should().Be("22");
        company.PagIbigNumber.Should().Be("33");
        company.AuthorizedSignatoryName.Should().Be("Jane");
        company.AuthorizedSignatoryTitle.Should().Be("HR Head");
    }

    [Fact]
    public async Task SeedDefaultAsync_NoProfile_SeedsNameOnly()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var service = new CompanyService(new UnitOfWorkService(context));

        await service.SeedDefaultAsync("Acme", null, CancellationToken.None);
        await service.CommitChangesAsync();

        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.Description.Should().Be("Acme");
        company.Address.Should().BeEmpty();
        company.TIN.Should().BeEmpty();
    }

    [Fact]
    public async Task Put_ChangedClassification_SyncsItToTenantService_AndSavesIt()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        await controller.Put(
            new CreateCompany
            {
                Description = "My Space",
                IndustryCode = "retail",
                SubIndustryCode = "retail-grocery",
                BusinessType = "corporation",
                CompanySize = "11-50",
            },
            CancellationToken.None);

        await tenantClient.Received(1).UpdateCurrentProfileAsync(
            Arg.Is<TenantProfileUpdate>(p => p.Classification != null
                && p.Classification.IndustryCode == "retail"
                && p.Classification.SubIndustryCode == "retail-grocery"
                && p.Classification.BusinessType == "corporation"
                && p.Classification.CompanySize == "11-50"),
            Arg.Any<CancellationToken>());
        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.IndustryCode.Should().Be("retail");
        company.SubIndustryCode.Should().Be("retail-grocery");
        company.BusinessType.Should().Be("corporation");
        company.CompanySize.Should().Be("11-50");
    }

    [Fact]
    public async Task Put_OnlyAddressChanged_DoesNotSendClassification()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        await controller.Put(new CreateCompany { Description = "My Space", Address = "1 Main St" }, CancellationToken.None);

        await tenantClient.Received(1).UpdateCurrentProfileAsync(
            Arg.Is<TenantProfileUpdate>(p => p.Classification == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_TenantServiceRefusesTheClassification_SavesNothing()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        var refusal = await ApiException.Create(
            new HttpRequestMessage(HttpMethod.Put, "http://tenant-api/api/v1/tenants/current/profile"),
            HttpMethod.Put,
            new HttpResponseMessage(HttpStatusCode.BadRequest),
            new RefitSettings());
        tenantClient.UpdateCurrentProfileAsync(Arg.Any<TenantProfileUpdate>(), Arg.Any<CancellationToken>()).ThrowsAsync(refusal);
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        var act = () => controller.Put(
            new CreateCompany { Description = "My Space", IndustryCode = "nope" }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nothing was saved*");
        await using var verify = db.NewContext();
        (await verify.Companies.SingleAsync()).IndustryCode.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedDefaultAsync_WithClassification_CopiesTheCodes()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();
        var service = new CompanyService(new UnitOfWorkService(context));

        await service.SeedDefaultAsync("Acme", new CompanyProfile
        {
            IndustryCode = "bpo",
            SubIndustryCode = "bpo-voice",
            BusinessType = "partnership",
            CompanySize = "51-200",
        }, CancellationToken.None);
        await service.CommitChangesAsync();

        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.IndustryCode.Should().Be("bpo");
        company.SubIndustryCode.Should().Be("bpo-voice");
        company.BusinessType.Should().Be("partnership");
        company.CompanySize.Should().Be("51-200");
    }

    [Fact]
    public void CompanyAddress_Compose_JoinsPartsAndSkipsBlanksAndTheDefaultCountry()
    {
        CompanyAddress.Compose(" 1 Main St ", "Brgy. Uno", "Makati", "Metro Manila", "1200", "Philippines")
            .Should().Be("1 Main St, Brgy. Uno, Makati, Metro Manila 1200");
        CompanyAddress.Compose("1 Main St", "", "Cebu", "", "", "Philippines").Should().Be("1 Main St, Cebu");
        CompanyAddress.Compose("1 Main St", null, null, null, null, "Japan").Should().Be("1 Main St, Japan");
        CompanyAddress.Compose("", "", "", "", "", "").Should().BeEmpty();
    }

    [Fact]
    public async Task Put_AddressParts_AreSaved_AndTheComposedAddressIsSyncedToTheConsole()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        await controller.Put(
            new CreateCompany
            {
                Description = "My Space",
                Address = "1 Main St",
                City = "Makati",
                Province = "Metro Manila",
                ZipCode = "1200",
                RegisteredName = "My Space Inc.",
                Website = "https://myspace.test",
                HrContactEmail = "hr@myspace.test",
                PayrollContactName = "Pay Roll",
                SecondaryPhone = "02-1234",
                WorkWeekRestDays = "Saturday,Sunday",
            },
            CancellationToken.None);

        await tenantClient.Received(1).UpdateCurrentProfileAsync(
            Arg.Is<TenantProfileUpdate>(p => p.Address == "1 Main St, Makati, Metro Manila 1200"),
            Arg.Any<CancellationToken>());
        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.Address.Should().Be("1 Main St");
        company.City.Should().Be("Makati");
        company.ZipCode.Should().Be("1200");
        company.RegisteredName.Should().Be("My Space Inc.");
        company.Website.Should().Be("https://myspace.test");
        company.HrContactEmail.Should().Be("hr@myspace.test");
        company.PayrollContactName.Should().Be("Pay Roll");
        company.SecondaryPhone.Should().Be("02-1234");
        company.WorkWeekRestDays.Should().Be("Saturday,Sunday");
    }

    [Fact]
    public async Task Put_OnlyRegionalOrContactFieldsChanged_DoesNotSyncTheConsoleCopy()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        await controller.Put(
            new CreateCompany { Description = "My Space", HrContactName = "Hr", TimeZone = "Asia/Manila", Website = "x.test" },
            CancellationToken.None);

        await tenantClient.DidNotReceive().UpdateCurrentProfileAsync(Arg.Any<TenantProfileUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Put_RegistrationAndPayslipFields_AreSaved_WithoutTouchingTheConsoleCopy()
    {
        using var db = await SeedCompanyAsync("My Space");
        var tenantClient = Substitute.For<ITenantClient>();
        await using var context = db.NewContext();
        var controller = new CompanySettingsController(
            new CompanyService(new UnitOfWorkService(context)), Substitute.For<IMapper>(), tenantClient,
            images: null!, imageUrls: null!);

        await controller.Put(
            new CreateCompany
            {
                Description = "My Space",
                EmployerType = "private",
                IsWithholdingAgent = true,
                AtcCode = "WC010",
                TinBranchCode = "00001",
                SssBranchCode = "01",
                PhilHealthBranchCode = "02",
                PagIbigBranchCode = "03",
                SecDtiNumber = "CS201912345",
                RegistrationDate = new DateTime(2019, 5, 20),
                BusinessPermitNo = "BP-2026-001",
                BusinessPermitExpiry = new DateTime(2026, 12, 31),
                PayslipFooter = "System-generated; no signature required.",
            },
            CancellationToken.None);

        await using var verify = db.NewContext();
        var company = await verify.Companies.SingleAsync();
        company.EmployerType.Should().Be("private");
        company.IsWithholdingAgent.Should().BeTrue();
        company.AtcCode.Should().Be("WC010");
        company.TinBranchCode.Should().Be("00001");
        company.SssBranchCode.Should().Be("01");
        company.PhilHealthBranchCode.Should().Be("02");
        company.PagIbigBranchCode.Should().Be("03");
        company.SecDtiNumber.Should().Be("CS201912345");
        company.RegistrationDate.Should().Be(new DateTime(2019, 5, 20));
        company.BusinessPermitNo.Should().Be("BP-2026-001");
        company.BusinessPermitExpiry.Should().Be(new DateTime(2026, 12, 31));
        company.PayslipFooter.Should().Be("System-generated; no signature required.");
        await tenantClient.DidNotReceive().UpdateCurrentProfileAsync(Arg.Any<TenantProfileUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NewCompany_DefaultsToThePhilippines()
    {
        var company = new Company();

        company.Country.Should().Be("Philippines");
        company.TimeZone.Should().Be("Asia/Manila");
        company.Currency.Should().Be("PHP");
        company.WorkWeekRestDays.Should().Be("Sunday");
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
