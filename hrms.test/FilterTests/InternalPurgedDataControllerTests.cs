using System.Reflection;
using Hrms.Api;
using Hrms.Api.Controllers;
using Hrms.Core.Services.Auditing;
using Hrms.Core.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Onepunch.AuditEngine;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.FilterTests;

/// <summary>What is kept of a purged company (its audit trail, the link to its archive) is read through an Api-key-only internal controller.</summary>
public class InternalPurgedDataControllerTests
{
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Controller_IsApiKeyOnly_AndOnlyReads()
    {
        var type = typeof(InternalPurgedDataController);

        type.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<ApiKeyAttribute>().Should().NotBeNull();
        foreach (var action in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            action.GetCustomAttributes<HttpMethodAttribute>().Should().OnlyContain(a => a is HttpGetAttribute);
    }

    [Fact]
    public async Task Audit_IsScopedToTheRoutesCompany()
    {
        var audits = Substitute.For<IAuditQueryService>();
        audits.QueryAsync(Arg.Any<AuditQuery>(), Arg.Any<CancellationToken>()).Returns(new AuditPage([], 0, 1, 20));
        var controller = new InternalPurgedDataController(audits, Substitute.For<IFileStorage>());

        await controller.Audit(CompanyA, new AuditLogQuery(), CancellationToken.None);

        await audits.Received(1).QueryAsync(
            Arg.Is<AuditQuery>(q => q.OrganizationId == CompanyA.ToString()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Audit_RefusesNoCompany()
    {
        var controller = new InternalPurgedDataController(Substitute.For<IAuditQueryService>(), Substitute.For<IFileStorage>());

        var act = () => controller.Audit(Guid.Empty, new AuditLogQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<GuardException>();
    }

    [Fact]
    public void ArchiveBelongsTo_OnlyAcceptsKeysInsideTheCompanysArchiveFolder()
    {
        var own = $"archives/{CompanyA:D}/company-archive-20261031.zip";

        InternalPurgedDataController.ArchiveBelongsTo(own, CompanyA).Should().BeTrue();
        InternalPurgedDataController.ArchiveBelongsTo(own, CompanyB).Should().BeFalse("it is another company's archive");
        InternalPurgedDataController.ArchiveBelongsTo($"tenants/{CompanyA:D}/employee-documents/x.pdf", CompanyA).Should().BeFalse("live files are not archives");
        InternalPurgedDataController.ArchiveBelongsTo($"archives/{CompanyA:D}/../{CompanyB:D}/x.zip", CompanyA).Should().BeFalse("no path tricks");
        InternalPurgedDataController.ArchiveBelongsTo(null, CompanyA).Should().BeFalse();
    }

    [Fact]
    public async Task ArchiveLink_GivesALinkForTheCompanysOwnArchive()
    {
        var key = $"archives/{CompanyA:D}/company-archive-20261031.zip";
        var storage = Substitute.For<IFileStorage>();
        storage.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(true);
        storage.GetDownloadUrlAsync(key, "company-archive-20261031.zip", Arg.Any<CancellationToken>())
            .Returns(new Uri("https://files.example/signed"));
        var controller = new InternalPurgedDataController(Substitute.For<IAuditQueryService>(), storage);

        var result = (OkObjectResult)await controller.ArchiveLink(CompanyA, key, CancellationToken.None);

        var link = result.Value.Should().BeOfType<InternalArchiveLink>().Subject;
        link.Url.Should().Be("https://files.example/signed");
        link.FileName.Should().Be("company-archive-20261031.zip");
    }

    [Fact]
    public async Task ArchiveLink_RefusesAnotherCompanysArchive_WithoutTouchingStorage()
    {
        var storage = Substitute.For<IFileStorage>();
        var controller = new InternalPurgedDataController(Substitute.For<IAuditQueryService>(), storage);

        var act = () => controller.ArchiveLink(CompanyA, $"archives/{CompanyB:D}/x.zip", CancellationToken.None);

        await act.Should().ThrowAsync<GuardException>();
        await storage.DidNotReceiveWithAnyArgs().GetDownloadUrlAsync(default!, default, default);
    }

    [Fact]
    public async Task ArchiveLink_ToldWhenTheArchiveIsGone()
    {
        var key = $"archives/{CompanyA:D}/x.zip";
        var storage = Substitute.For<IFileStorage>();
        storage.ExistsAsync(key, Arg.Any<CancellationToken>()).Returns(false);
        var controller = new InternalPurgedDataController(Substitute.For<IAuditQueryService>(), storage);

        var act = () => controller.ArchiveLink(CompanyA, key, CancellationToken.None);

        await act.Should().ThrowAsync<GuardException>().WithMessage("*no longer exists*");
    }
}
