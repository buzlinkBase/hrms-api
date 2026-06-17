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
        //// 1. Get the path to Hrms.Api properly
        string currentDirectory = Directory.GetCurrentDirectory();
        string basePath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "hrms-adms-api"));
        if (!Directory.Exists(basePath))
        {
            basePath = Path.GetFullPath(Path.Combine(currentDirectory), "hrms-adms-api");
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("AdmsConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not find 'connection string'. Check your appsettings.json path.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AdmsContext>();
        var serverVersion = new MySqlServerVersion(new Version(9, 2, 0));
        optionsBuilder.UseMySql(connectionString, serverVersion);
        return new AdmsContext(optionsBuilder.Options, new DesignTimeTenantProvider());
    }
}
