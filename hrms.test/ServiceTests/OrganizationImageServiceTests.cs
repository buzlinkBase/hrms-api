using BuzlinkRepository;
using Hrms.Core.Services;
using Hrms.Core.Services.Storage;
using Hrms.Domain.ValueObjects;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using Hrms.Infrastructure;
using hrms.test.InfrastructureTests;
using hrms.test.TestSupport;
using Microsoft.EntityFrameworkCore;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>What counts as an image: the file's own bytes decide, never its name or claimed type.</summary>
public class ImageUploadsTests
{
    internal static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    internal static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];
    internal static readonly byte[] Webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, 1, 2];

    [Fact]
    public void RecognizesPngJpegAndWebp_AndNothingElse()
    {
        ImageUploads.DetectContentType(Png).Should().Be("image/png");
        ImageUploads.DetectContentType(Jpeg).Should().Be("image/jpeg");
        ImageUploads.DetectContentType(Webp).Should().Be("image/webp");
        ImageUploads.DetectContentType("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8).Should().BeNull("SVG can carry script");
        ImageUploads.DetectContentType("%PDF-1.7"u8).Should().BeNull();
    }

    [Fact]
    public async Task RejectsEmpty_TooLarge_AndNonImageFiles_WithAMessageForTheUser()
    {
        var empty = () => ImageUploads.ReadAsync(new MemoryStream(), 0, default);
        var declaredTooLarge = () => ImageUploads.ReadAsync(new MemoryStream(Png), ImageUploads.MaxBytes + 1, default);
        // A stream that's bigger than it claimed is caught while reading.
        var actuallyTooLarge = () => ImageUploads.ReadAsync(new MemoryStream([.. Png, .. new byte[ImageUploads.MaxBytes]]), null, default);
        var notAnImage = () => ImageUploads.ReadAsync(new MemoryStream("hello"u8.ToArray()), 5, default);

        await empty.Should().ThrowAsync<InvalidImageException>();
        (await declaredTooLarge.Should().ThrowAsync<InvalidImageException>()).WithMessage("*2 MB*");
        await actuallyTooLarge.Should().ThrowAsync<InvalidImageException>();
        (await notAnImage.Should().ThrowAsync<InvalidImageException>()).WithMessage("Upload a PNG, JPG or WebP image.");
    }

    [Fact]
    public void AnInvalidImage_IsABadRequest_NotAServerError()
    {
        new InvalidImageException("x").Should().BeAssignableTo<GuardException>();
    }
}

/// <summary>
/// Company logos and employee photos live in file storage under the company; the database keeps
/// only the key, replaced files are deleted, and the regular saves never touch them.
/// </summary>
public class OrganizationImageServiceTests : IDisposable
{
    private static readonly Guid Company = Guid.NewGuid();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-images-{Guid.NewGuid():N}");
    private readonly SqliteHrmsContext _db = new();

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class CompanyTenant : ITenantProvider
    {
        public Guid TenantId { get; private set; } = Company;
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    }

    private LocalFileStorage Storage => new(_root);

    /// <summary>Runs one request with the service, closed before the next (a unit of work holds a write lock).</summary>
    private async Task<T> WithServiceAsync<T>(Func<OrganizationImageService, Task<T>> act, IConfidentialAccess? access = null)
    {
        await using var context = access == null ? _db.NewContext() : _db.NewContext(access);
        var storage = Storage;
        return await act(new OrganizationImageService(new UnitOfWorkService(context), storage, new CompanyTenant(), new ImageUrlResolver(storage)));
    }

    private async Task<Employee> SeedEmployeeAsync(bool confidential = false, string profileImg = "")
    {
        var groupId = Guid.NewGuid();
        var employee = new Employee
        {
            Id = Guid.NewGuid(), PayrollGroupId = groupId, JobLevel = "", FirstName = "Ana", LastName = "Reyes",
            BioId = Random.Shared.Next(1, int.MaxValue), IsConfidential = confidential, ProfileImg = profileImg,
        };
        await using var seed = _db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = groupId, Name = "Main" });
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    private async Task<string> StoredPhotoAsync(Guid employeeId)
    {
        await using var read = _db.NewContext();
        return (await read.Employees.AsNoTracking().SingleAsync(e => e.Id == employeeId)).ProfileImg;
    }

    private async Task<string?> StoredLogoAsync()
    {
        await using var read = _db.NewContext();
        return (await read.Companies.AsNoTracking().SingleOrDefaultAsync())?.LogoKey;
    }

    private string FileOf(string key) => Path.Combine(_root, key);

    [Fact]
    public async Task APhoto_IsStoredUnderTheCompany_AndShownRightAway()
    {
        var ana = await SeedEmployeeAsync();

        var url = await WithServiceAsync(s => s.SetEmployeePhotoAsync(ana.Id, new MemoryStream(ImageUploadsTests.Png), 12, "me.PNG", default));

        var key = await StoredPhotoAsync(ana.Id);
        key.Should().StartWith($"tenants/{Company}/employee-photos/").And.EndWith("-me.png");
        File.Exists(FileOf(key)).Should().BeTrue();
        url.Should().StartWith("data:image/png;base64,", "local storage shows the image inline");
    }

    [Fact]
    public async Task TheFileType_ComesFromItsBytes_NotItsName()
    {
        var ana = await SeedEmployeeAsync();

        await WithServiceAsync(s => s.SetEmployeePhotoAsync(ana.Id, new MemoryStream(ImageUploadsTests.Jpeg), 8, "photo.png", default));

        (await StoredPhotoAsync(ana.Id)).Should().EndWith("-photo.jpg");
    }

    [Fact]
    public async Task ReplacingOrRemovingAPhoto_DeletesTheOldFile()
    {
        var ana = await SeedEmployeeAsync();
        await WithServiceAsync(s => s.SetEmployeePhotoAsync(ana.Id, new MemoryStream(ImageUploadsTests.Png), 12, "a.png", default));
        var first = await StoredPhotoAsync(ana.Id);

        await WithServiceAsync(s => s.SetEmployeePhotoAsync(ana.Id, new MemoryStream(ImageUploadsTests.Webp), 14, "b.webp", default));
        var second = await StoredPhotoAsync(ana.Id);
        File.Exists(FileOf(first)).Should().BeFalse();
        File.Exists(FileOf(second)).Should().BeTrue();

        await WithServiceAsync(async s => { await s.RemoveEmployeePhotoAsync(ana.Id, default); return true; });
        (await StoredPhotoAsync(ana.Id)).Should().BeEmpty();
        File.Exists(FileOf(second)).Should().BeFalse();
    }

    [Fact]
    public async Task ReplacingAnOldInlinePhoto_Works()
    {
        var ana = await SeedEmployeeAsync(profileImg: "data:image/png;base64,AAAA");

        await WithServiceAsync(s => s.SetEmployeePhotoAsync(ana.Id, new MemoryStream(ImageUploadsTests.Png), 12, "new.png", default));

        (await StoredPhotoAsync(ana.Id)).Should().StartWith("tenants/");
    }

    [Fact]
    public async Task ARejectedUpload_ChangesNothing()
    {
        var ana = await SeedEmployeeAsync();

        var act = () => WithServiceAsync(s => s.SetEmployeePhotoAsync(ana.Id, new MemoryStream("not an image"u8.ToArray()), 12, "x.png", default));

        await act.Should().ThrowAsync<InvalidImageException>();
        (await StoredPhotoAsync(ana.Id)).Should().BeEmpty();
        Directory.Exists(_root).Should().BeFalse("nothing was stored");
    }

    [Fact]
    public async Task AConfidentialEmployee_TheCallerCantSee_IsNotFound()
    {
        var secret = await SeedEmployeeAsync(confidential: true);

        var act = () => WithServiceAsync(
            s => s.SetEmployeePhotoAsync(secret.Id, new MemoryStream(ImageUploadsTests.Png), 12, "x.png", default),
            new AnalyticsSeed.Uncleared(secret.Id));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TheLogo_IsStoredReplacedAndRemoved()
    {
        await using (var seed = _db.NewContext())
        {
            seed.Companies.Add(new Company { Id = Guid.NewGuid(), Code = "Main", Description = "Acme" });
            await seed.SaveChangesAsync();
        }

        await WithServiceAsync(s => s.SetCompanyLogoAsync(new MemoryStream(ImageUploadsTests.Png), 12, "acme logo.png", default));
        var first = await StoredLogoAsync();
        first.Should().StartWith($"tenants/{Company}/company-logo/").And.EndWith("-acme-logo.png");

        await WithServiceAsync(s => s.SetCompanyLogoAsync(new MemoryStream(ImageUploadsTests.Png), 12, "v2.png", default));
        File.Exists(FileOf(first!)).Should().BeFalse();

        await WithServiceAsync(async s => { await s.RemoveCompanyLogoAsync(default); return true; });
        (await StoredLogoAsync()).Should().BeNull();
    }

    [Fact]
    public async Task SavingCompanyInfo_KeepsTheLogo()
    {
        await using (var seed = _db.NewContext())
        {
            seed.Companies.Add(new Company { Id = Guid.NewGuid(), Code = "Main", Description = "Acme", LogoKey = $"tenants/{Company}/company-logo/x.png" });
            await seed.SaveChangesAsync();
        }

        await using (var context = _db.NewContext())
            await new CompanyService(new UnitOfWorkService(context)).SaveAsync(new CreateCompany { Description = "Acme Inc." }, default);

        (await StoredLogoAsync()).Should().Be($"tenants/{Company}/company-logo/x.png");
    }

    private async Task<Client> SeedClientAsync(string? logoKey = null)
    {
        var client = new Client { Id = Guid.NewGuid(), Code = "ACME", Name = "Acme", LogoKey = logoKey };
        await using var seed = _db.NewContext();
        seed.Clients.Add(client);
        await seed.SaveChangesAsync();
        return client;
    }

    private async Task<string?> StoredClientLogoAsync(Guid clientId)
    {
        await using var read = _db.NewContext();
        return (await read.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId)).LogoKey;
    }

    [Fact]
    public async Task AClientLogo_IsStoredReplacedAndRemoved()
    {
        var acme = await SeedClientAsync();

        var url = await WithServiceAsync(s => s.SetClientLogoAsync(acme.Id, new MemoryStream(ImageUploadsTests.Png), 12, "acme logo.png", default));
        var first = await StoredClientLogoAsync(acme.Id);
        first.Should().StartWith($"tenants/{Company}/client-logos/").And.EndWith("-acme-logo.png");
        url.Should().StartWith("data:image/png;base64,");

        await WithServiceAsync(s => s.SetClientLogoAsync(acme.Id, new MemoryStream(ImageUploadsTests.Png), 12, "v2.png", default));
        var second = await StoredClientLogoAsync(acme.Id);
        File.Exists(FileOf(first!)).Should().BeFalse();
        File.Exists(FileOf(second!)).Should().BeTrue();

        await WithServiceAsync(async s => { await s.RemoveClientLogoAsync(acme.Id, default); return true; });
        (await StoredClientLogoAsync(acme.Id)).Should().BeNull();
        File.Exists(FileOf(second!)).Should().BeFalse();
    }

    [Fact]
    public async Task AClientLogo_ForAnUnknownClient_IsNotFound()
    {
        var act = () => WithServiceAsync(s => s.SetClientLogoAsync(Guid.NewGuid(), new MemoryStream(ImageUploadsTests.Png), 12, "x.png", default));

        await act.Should().ThrowAsync<NotFoundException>();
        Directory.Exists(_root).Should().BeFalse("nothing was stored");
    }

    [Fact]
    public async Task SavingAClient_KeepsTheLogo()
    {
        var key = $"tenants/{Company}/client-logos/2026/10/abc-acme.png";
        var acme = await SeedClientAsync(key);

        await using (var context = _db.NewContext())
            await new ClientService(new UnitOfWorkService(context)).UpdateAsync(
                new UpdateClient { Id = acme.Id, Code = "ACME", Name = "Acme Inc." }, default);

        (await StoredClientLogoAsync(acme.Id)).Should().Be(key);
    }

    [Fact]
    public async Task SavingAnEmployee_KeepsThePhoto_WhateverTheFormSendsBack()
    {
        var key = $"tenants/{Company}/employee-photos/2026/10/abc-me.png";
        var ana = await SeedEmployeeAsync(profileImg: key);

        await using (var context = _db.NewContext())
        {
            var service = ConfidentialAccessTests.EmployeeServiceFor(context, scope: null);
            await service.UpdateAsync(new UpdateEmployee
            {
                Id = ana.Id, FirstName = "Ana", LastName = "Reyes", JobLevel = "", PayrollGroupId = ana.PayrollGroupId,
                BioId = ana.BioId, RestDays = [],
                // e.g. the signed display link the form was given
                ProfileImg = "https://onepunch-files.sgp1.digitaloceanspaces.com/hrms/x?X-Amz-Signature=abc",
            }, default);
            await service.CommitChangesAsync(default);
        }

        (await StoredPhotoAsync(ana.Id)).Should().Be(key);
    }
}

public class ImageUrlResolverTests
{
    [Fact]
    public async Task OldInlinePhotos_PassThrough_AndBadOrMissingKeysShowNothing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"hrms-urls-{Guid.NewGuid():N}");
        var resolver = new ImageUrlResolver(new LocalFileStorage(root));

        (await resolver.DisplayUrlAsync("data:image/png;base64,AAAA")).Should().Be("data:image/png;base64,AAAA");
        (await resolver.DisplayUrlAsync(null)).Should().BeEmpty();
        (await resolver.DisplayUrlAsync("../etc/passwd")).Should().BeEmpty();
        (await resolver.DisplayUrlAsync("tenants/x/missing.png")).Should().BeEmpty();
    }
}
