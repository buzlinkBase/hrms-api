using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Hrms.Core.Services.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>Storage keys keep every company's files under that company, and nothing escapes its folder.</summary>
public class StorageKeysTests
{
    private static readonly Guid Acme = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void NewFiles_GoUnderTheirCompany_AreaAndMonth_WithAUniqueId()
    {
        var at = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var first = StorageKeys.ForNewFile(Acme, "employee-documents", "TOR 2025.PDF", at);
        var second = StorageKeys.ForNewFile(Acme, "employee-documents", "TOR 2025.PDF", at);

        first.Should().MatchRegex(@"^tenants/11111111-1111-1111-1111-111111111111/employee-documents/2026/10/[0-9a-f]{32}-TOR-2025\.pdf$");
        second.Should().NotBe(first, "two uploads of the same name never overwrite each other");
        StorageKeys.BelongsTo(first, Acme).Should().BeTrue();
        StorageKeys.BelongsTo(first, Other).Should().BeFalse();
    }

    [Fact]
    public void AFileWithoutACompanyOrArea_IsRefused()
    {
        var noCompany = () => StorageKeys.ForNewFile(Guid.Empty, "exports", "a.xlsx");
        var noArea = () => StorageKeys.ForNewFile(Acme, " / ", "a.xlsx");

        noCompany.Should().Throw<ArgumentException>();
        noArea.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Juan Dela Cruz – Résumé.docx", "Juan-Dela-Cruz-Resume.docx")]
    [InlineData(@"C:\fakepath\../../etc/passwd", "passwd")]
    [InlineData("photo.JPEG", "photo.jpeg")]
    [InlineData("???", "file")]
    [InlineData(null, "file")]
    public void FileNames_AreMadeSafe(string? name, string expected) =>
        StorageKeys.SafeFileName(name).Should().Be(expected);

    [Theory]
    [InlineData("tenants/x/../y/file.pdf")]
    [InlineData("/tenants/x/file.pdf")]
    [InlineData(@"tenants\x\file.pdf")]
    [InlineData("tenants//file.pdf")]
    [InlineData("")]
    public void MalformedKeys_AreInvalid(string key) => StorageKeys.IsValid(key).Should().BeFalse();

    [Fact]
    public void AnotherCompanysKey_DoesNotBelongToThisOne_EvenWithTraversal()
    {
        var sneaky = $"tenants/{Acme}/../{Other}/payroll.pdf";

        StorageKeys.BelongsTo(sneaky, Acme).Should().BeFalse();
    }
}

public class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"hrms-storage-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task StoresReadsAndDeletesAFile()
    {
        var storage = new LocalFileStorage(_root);
        var key = StorageKeys.ForNewFile(Guid.NewGuid(), "exports", "report.csv");

        var stored = await storage.UploadAsync(new FileUpload(key, new MemoryStream("a,b"u8.ToArray()), "text/csv"));

        stored.Should().Be(new StoredFile(key, 3, "text/csv"));
        (await storage.ExistsAsync(key)).Should().BeTrue();
        await using (var content = await storage.OpenReadAsync(key))
            new StreamReader(content!).ReadToEnd().Should().Be("a,b");
        (await storage.GetDownloadUrlAsync(key)).Should().BeNull("local disk can't hand out links");

        await storage.DeleteAsync(key);
        (await storage.ExistsAsync(key)).Should().BeFalse();
        (await storage.OpenReadAsync(key)).Should().BeNull();
        await storage.DeleteAsync(key); // already gone: no error
    }

    [Fact]
    public async Task AKeyCannotReachOutsideTheFolder()
    {
        var storage = new LocalFileStorage(_root);

        var act = () => storage.OpenReadAsync("tenants/../../secrets.txt");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}

public class SpacesFileStorageTests
{
    private static SpacesStorageOptions Spaces(Action<SpacesStorageOptions>? change = null)
    {
        var options = new SpacesStorageOptions
        {
            Endpoint = "https://sgp1.digitaloceanspaces.com",
            Bucket = "onepunch-files",
            AccessKey = "DO00TESTKEY",
            SecretKey = "test-secret",
            RootPrefix = "hrms",
            DownloadUrlMinutes = 15,
        };
        change?.Invoke(options);
        return options;
    }

    private static SpacesFileStorage Sut(IAmazonS3 s3, SpacesStorageOptions? spaces = null) =>
        new(s3, Options.Create(new StorageOptions { Provider = StorageProviders.Spaces, Spaces = spaces ?? Spaces() }));

    [Fact]
    public async Task Uploads_ArePrivate_InTheSpace_UnderTheRootFolder()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var key = StorageKeys.ForNewFile(Guid.NewGuid(), "employee-documents", "contract.pdf");

        var stored = await Sut(s3).UploadAsync(new FileUpload(key, new MemoryStream(new byte[42]), "application/pdf"));

        stored.Size.Should().Be(42);
        await s3.Received(1).PutObjectAsync(
            Arg.Is<PutObjectRequest>(r =>
                r.BucketName == "onepunch-files" &&
                r.Key == $"hrms/{key}" &&
                r.CannedACL == S3CannedACL.Private &&
                r.ContentType == "application/pdf"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AMissingFile_ReadsAsNothing()
    {
        var s3 = Substitute.For<IAmazonS3>();
        s3.GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<GetObjectResponse>(_ => throw new AmazonS3Exception("Not Found") { StatusCode = System.Net.HttpStatusCode.NotFound });

        (await Sut(s3).OpenReadAsync("tenants/x/a.pdf")).Should().BeNull();
    }

    [Fact]
    public async Task DownloadLinks_AreSignedForTheSpace_ExpireAndNameTheFile()
    {
        // Signing happens locally -- no request reaches DigitalOcean.
        using var s3 = SpacesFileStorage.CreateClient(Spaces());

        var url = await Sut(s3).GetDownloadUrlAsync("tenants/x/9c-contract.pdf", "Ana's contract.pdf");

        url!.Host.Should().Be("onepunch-files.sgp1.digitaloceanspaces.com");
        url.AbsolutePath.Should().Be("/hrms/tenants/x/9c-contract.pdf");
        var query = Uri.UnescapeDataString(url.Query);
        query.Should().Contain("X-Amz-Expires=900");
        query.Should().Contain("attachment; filename=\"Ana-s-contract.pdf\"");
    }

    [Fact]
    public void TheClient_OnlySendsChecksumsWhenRequired_WhichSpacesSupports()
    {
        var config = SpacesFileStorage.ClientConfig(Spaces());

        config.ServiceURL.Should().StartWith("https://sgp1.digitaloceanspaces.com");
        config.RequestChecksumCalculation.Should().Be(RequestChecksumCalculation.WHEN_REQUIRED);
        config.ResponseChecksumValidation.Should().Be(ResponseChecksumValidation.WHEN_REQUIRED);
    }

    [Fact]
    public void MissingSettings_AreAllNamed()
    {
        var problems = Spaces(o => { o.Endpoint = "http://insecure"; o.Bucket = ""; o.AccessKey = ""; o.SecretKey = ""; }).Problems();

        problems.Should().HaveCount(4);
        Spaces().Problems().Should().BeEmpty();
    }
}

public class StorageRegistrationTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => $"Storage:{v.Key}", v => (string?)v.Value))
            .Build();

    [Fact]
    public void Spaces_IsUsed_WhenConfigured()
    {
        var services = new ServiceCollection().AddFileStorage(Config(
            ("Provider", "Spaces"),
            ("Spaces:Endpoint", "https://sgp1.digitaloceanspaces.com"),
            ("Spaces:Bucket", "onepunch-files"),
            ("Spaces:AccessKey", "key"),
            ("Spaces:SecretKey", "secret")));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IFileStorage>().Should().BeOfType<SpacesFileStorage>();
    }

    [Fact]
    public void Spaces_WithoutKeys_FailsAtStartup_SayingWhatsMissing()
    {
        var act = () => new ServiceCollection().AddFileStorage(Config(
            ("Provider", "Spaces"),
            ("Spaces:Endpoint", "https://sgp1.digitaloceanspaces.com"),
            ("Spaces:Bucket", "onepunch-files")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Storage__Spaces__AccessKey*");
    }

    [Fact]
    public void AnUnknownProvider_FailsAtStartup()
    {
        var act = () => new ServiceCollection().AddFileStorage(Config(("Provider", "Dropbox")));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Local_IsTheDefault()
    {
        var services = new ServiceCollection().AddFileStorage(Config());

        services.Should().Contain(d => d.ServiceType == typeof(IFileStorage) && d.ImplementationType == typeof(LocalFileStorage));
    }
}
