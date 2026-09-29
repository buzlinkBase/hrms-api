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
            //.AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            ; 
        var apiAssembly = typeof(Program).Assembly; 
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
        var sqlVersion = new MySqlServerVersion(new Version(8, 4, 8));
        optionsBuilder.UseMySql(connectionString, sqlVersion);
        return new AdmsContext(optionsBuilder.Options, new DesignTimeTenantProvider());
    }
}
