using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Hrms.Core.Services.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IFileStorage"/> from the "Storage" section: DigitalOcean Spaces when
    /// Provider is "Spaces" (checked at startup -- a missing key fails the app now, not the first
    /// upload), otherwise a local folder.
    /// </summary>
    public static IServiceCollection AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(StorageOptions.SectionName);
        services.Configure<StorageOptions>(section);
        var options = section.Get<StorageOptions>() ?? new StorageOptions();

        if (string.Equals(options.Provider, StorageProviders.Spaces, StringComparison.OrdinalIgnoreCase))
        {
            var problems = options.Spaces.Problems();
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    "DigitalOcean Spaces storage isn't configured: " + string.Join(" ", problems));

            // One client for the app's lifetime -- it's thread-safe and pools its connections.
            services.AddSingleton<IAmazonS3>(_ => SpacesFileStorage.CreateClient(options.Spaces));
            services.AddSingleton<IFileStorage, SpacesFileStorage>();
            Log.Information("File storage: DigitalOcean Spaces bucket {Bucket} at {Endpoint}",
                options.Spaces.Bucket, options.Spaces.Endpoint);
        }
        else if (string.Equals(options.Provider, StorageProviders.Local, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
            Log.Warning("File storage: local folder {RootPath} -- for development only; use Spaces in production",
                options.Local.RootPath);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown Storage:Provider \"{options.Provider}\" -- use \"{StorageProviders.Spaces}\" or \"{StorageProviders.Local}\".");
        }
        // Shows stored logos and photos (signed links, or inline images on local disk).
        services.AddSingleton<ImageUrlResolver>();
        // How much each company has stored -- what the plan's storage limit is checked against.
        services.AddSingleton<IStorageUsageMeter, StorageUsageMeter>();
        return services;
    }
}
