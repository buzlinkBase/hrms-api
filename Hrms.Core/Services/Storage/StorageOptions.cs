namespace Hrms.Core.Services.Storage;

/// <summary>
/// Settings bound from the "Storage" section. Secrets never go in appsettings: in production they
/// come from environment variables (docker-compose's .env), which ASP.NET binds by name --
/// e.g. <c>Storage__Spaces__AccessKey</c> and <c>Storage__Spaces__SecretKey</c>.
/// </summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"Spaces" (DigitalOcean Spaces) in production; "Local" (a folder on disk) for development and tests.</summary>
    public string Provider { get; set; } = StorageProviders.Local;

    public SpacesStorageOptions Spaces { get; set; } = new();
    public LocalStorageOptions Local { get; set; } = new();
}

public static class StorageProviders
{
    public const string Spaces = "Spaces";
    public const string Local = "Local";
}

public class SpacesStorageOptions
{
    /// <summary>The region endpoint, e.g. https://sgp1.digitaloceanspaces.com (no bucket name in it).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The Space's name (the S3 "bucket").</summary>
    public string Bucket { get; set; } = string.Empty;

    /// <summary>The region requests are signed for. Spaces accepts us-east-1 for every region.</summary>
    public string SigningRegion { get; set; } = "us-east-1";

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Folder every key lives under, so one Space can also hold other apps' or environments'
    /// files (e.g. "hrms" in production, "hrms-staging" on staging).
    /// </summary>
    public string RootPrefix { get; set; } = "hrms";

    /// <summary>How long a download link works.</summary>
    public int DownloadUrlMinutes { get; set; } = 15;

    /// <summary>What's missing for Spaces to work, or empty when it's ready.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
            problems.Add("Storage:Spaces:Endpoint must be an https URL, e.g. https://sgp1.digitaloceanspaces.com.");
        if (string.IsNullOrWhiteSpace(Bucket)) problems.Add("Storage:Spaces:Bucket is required.");
        if (string.IsNullOrWhiteSpace(AccessKey)) problems.Add("Storage:Spaces:AccessKey is required (set Storage__Spaces__AccessKey).");
        if (string.IsNullOrWhiteSpace(SecretKey)) problems.Add("Storage:Spaces:SecretKey is required (set Storage__Spaces__SecretKey).");
        if (DownloadUrlMinutes is < 1 or > 7 * 24 * 60) problems.Add("Storage:Spaces:DownloadUrlMinutes must be between 1 and 10080 (7 days).");
        return problems;
    }
}

public class LocalStorageOptions
{
    /// <summary>Folder files are kept in; relative paths are under the app's content root. Never under wwwroot.</summary>
    public string RootPath { get; set; } = "storage";
}
