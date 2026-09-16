using Microsoft.EntityFrameworkCore.Design;

namespace Hrms.adms.Insfrastructure;

file sealed class DesignTimeTenantProvider : ITenantProvider
{
    public Guid TenantId => Guid.Empty;
    public void SetTenantId(Guid tenantId) { }
}

public class AdmsContextFactory : IDesignTimeDbContextFactory<AdmsContext>
{
    public AdmsContext CreateDbContext(string[] args)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string basePath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "hrms-adms-api"));
        if (!Directory.Exists(basePath))
        {
            basePath = Path.GetFullPath(Path.Combine(currentDirectory, "hrms-adms-api"));
        }

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            //.AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            ;

        // FIX: Extract assembly directly from a type inside the API project.
        // Option A: Reference the Program class from your API namespace directly
        var apiAssembly = typeof(Program).Assembly;

        // Option B: If 'Program' is hidden (top-level statements), create a blank public 
        // dummy class inside your API project named 'ApiMarker' and reference it:
        // var apiAssembly = typeof(Hrms.adms.Api.ApiMarker).Assembly;

        if (apiAssembly != null)
        {
            configBuilder.AddUserSecrets(apiAssembly, optional: true);
        }

        var configuration = configBuilder.Build();

        var connectionString = configuration.GetConnectionString("AdmsConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not find 'AdmsConnection'. Check your appsettings or secrets.json.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AdmsContext>();
        var serverVersion = ServerVersion.AutoDetect(connectionString);
        optionsBuilder.UseMySql(connectionString, serverVersion);

        return new AdmsContext(optionsBuilder.Options, new DesignTimeTenantProvider());
    }
}
