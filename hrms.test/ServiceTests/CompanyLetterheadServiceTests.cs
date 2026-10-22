using Hrms.Core.Services.Storage;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>The logo bytes a payslip letterhead prints: only when switched on, uploaded, readable and small.</summary>
public class CompanyLetterheadServiceTests
{
    private static readonly string Key = StorageKeys.ForNewFile(Guid.NewGuid(), OrganizationImageService.LogoArea, "logo.png");

    private static (CompanyLetterheadService Sut, IFileStorage Storage) Build(byte[]? stored)
    {
        var storage = Substitute.For<IFileStorage>();
        storage.OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Stream?>(stored == null ? null : new MemoryStream(stored)));
        return (new CompanyLetterheadService(storage), storage);
    }

    [Fact]
    public async Task OnWithALogo_ReturnsTheBytes()
    {
        var (sut, _) = Build([1, 2, 3]);

        (await sut.LoadLogoAsync(new Company { LogoKey = Key, ShowLetterhead = true })).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task SwitchedOff_ReturnsNothing_WithoutTouchingStorage()
    {
        var (sut, storage) = Build([1, 2, 3]);

        (await sut.LoadLogoAsync(new Company { LogoKey = Key, ShowLetterhead = false })).Should().BeNull();
        await storage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoLogo_NoCompany_OrAMissingFile_ReturnNothing()
    {
        var (sut, _) = Build([1, 2, 3]);
        var (missing, _) = Build(null);

        (await sut.LoadLogoAsync(new Company { LogoKey = null })).Should().BeNull();
        (await sut.LoadLogoAsync(null)).Should().BeNull();
        (await missing.LoadLogoAsync(new Company { LogoKey = Key })).Should().BeNull();
    }

    [Fact]
    public async Task AnOversizedFile_IsLeftOut()
    {
        var (sut, _) = Build(new byte[CompanyLetterheadService.MaxLogoBytes + 1]);

        (await sut.LoadLogoAsync(new Company { LogoKey = Key })).Should().BeNull();
    }

    [Fact]
    public async Task AStorageFailure_DoesNotStopThePayslip()
    {
        var storage = Substitute.For<IFileStorage>();
        storage.OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<Stream?>>(_ => throw new IOException("down"));

        (await new CompanyLetterheadService(storage).LoadLogoAsync(new Company { LogoKey = Key })).Should().BeNull();
    }
}
