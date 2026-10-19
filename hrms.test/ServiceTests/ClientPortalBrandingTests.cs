using Hrms.Core.Services;
using Hrms.Core.Services.ClientPortal;
using Hrms.Core.Services.Storage;
using Hrms.Domain.Entities;
using hrms.test.TestSupport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.ServiceTests;

/// <summary>The company's name and logo, sent to the Client Portal so an agency's clients see its brand.</summary>
public class ClientPortalBrandingTests : IDisposable
{
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-branding-{Guid.NewGuid():N}");
    private readonly SqliteHrmsContext _db = new();
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly FakePortal _portal = new();

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class FakePortal : IClientPortalClient
    {
        public List<(string Resource, object Row)> Sent { get; } = [];
        public bool Fail { get; set; }
        public int Rejected { get; set; }

        public Task<PortalIngestResult> PutAsync<T>(ClientPortalCredentials credentials, string resource, IReadOnlyList<T> rows, CancellationToken token)
        {
            if (Fail) throw new HttpRequestException("portal down");
            Sent.AddRange(rows.Select(r => (resource, (object)r!)));
            return Task.FromResult(new PortalIngestResult(Rejected > 0 ? 0 : rows.Count, Rejected, Rejected > 0 ? [new PortalRowError("branding", "no")] : []));
        }
    }

    private async Task ConnectAsync()
    {
        await using var ctx = _db.NewContext();
        await new ClientPortalSettingsService(new UnitOfWorkService(ctx), _protection)
            .SaveAsync(new ClientPortalSettingsRequest(true, "https://portal.example.com", "cpk_abcd.secret"), CancellationToken.None);
    }

    private async Task SeedCompanyAsync(string description, string shortName = "", byte[]? logo = null)
    {
        string? key = null;
        if (logo != null)
        {
            key = StorageKeys.ForNewFile(Company, "company-logo", "logo.png");
            await new LocalFileStorage(_root).UploadAsync(new FileUpload(key, new MemoryStream(logo), "image/png"), CancellationToken.None);
        }
        await using var ctx = _db.NewContext();
        ctx.Companies.Add(new Company { Code = "Main", Description = description, ShortName = shortName, LogoKey = key });
        await ctx.SaveChangesAsync();
    }

    private async Task<bool> PublishAsync()
    {
        await using var ctx = _db.NewContext();
        var uow = new UnitOfWorkService(ctx);
        var service = new ClientPortalBrandingService(
            uow, new ClientPortalSettingsService(uow, _protection), _portal, new LocalFileStorage(_root), NullLogger<ClientPortalBrandingService>.Instance);
        return await service.PublishAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Sends_the_trimmed_company_name_and_the_logo_file()
    {
        await ConnectAsync();
        await SeedCompanyAsync("  Acme Security Agency ", logo: Png);

        (await PublishAsync()).Should().BeTrue();

        var (resource, row) = _portal.Sent.Single();
        resource.Should().Be("branding");
        var branding = row.Should().BeOfType<PortalBrandingRow>().Subject;
        branding.Name.Should().Be("Acme Security Agency");
        Convert.FromBase64String(branding.LogoBase64!).Should().Equal(Png);
    }

    [Fact]
    public async Task A_company_without_a_logo_is_sent_with_none_so_the_portal_drops_the_old_one()
    {
        await ConnectAsync();
        await SeedCompanyAsync("Acme");

        (await PublishAsync()).Should().BeTrue();

        ((PortalBrandingRow)_portal.Sent.Single().Row).LogoBase64.Should().BeNull();
    }

    [Fact]
    public async Task The_short_name_stands_in_when_the_company_name_is_blank_and_nothing_is_sent_when_both_are()
    {
        await ConnectAsync();
        await SeedCompanyAsync("  ", "ACME");
        (await PublishAsync()).Should().BeTrue();
        ((PortalBrandingRow)_portal.Sent.Single().Row).Name.Should().Be("ACME");

        _portal.Sent.Clear();
        await using (var ctx = _db.NewContext())
        {
            var company = await ctx.Companies.SingleAsync();
            company.ShortName = "";
            await ctx.SaveChangesAsync();
        }
        (await PublishAsync()).Should().BeFalse();
        _portal.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Sends_nothing_while_the_portal_connection_is_off()
    {
        await SeedCompanyAsync("Acme", logo: Png);

        (await PublishAsync()).Should().BeFalse();

        _portal.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task A_portal_that_is_down_or_refuses_never_throws_into_the_company_edit()
    {
        await ConnectAsync();
        await SeedCompanyAsync("Acme", logo: Png);

        _portal.Fail = true;
        (await PublishAsync()).Should().BeFalse();

        _portal.Fail = false;
        _portal.Rejected = 1;
        (await PublishAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task A_logo_file_that_is_gone_is_left_out_rather_than_failing()
    {
        await ConnectAsync();
        await SeedCompanyAsync("Acme", logo: Png);
        Directory.Delete(_root, recursive: true);

        (await PublishAsync()).Should().BeTrue();

        ((PortalBrandingRow)_portal.Sent.Single().Row).LogoBase64.Should().BeNull();
    }
}
