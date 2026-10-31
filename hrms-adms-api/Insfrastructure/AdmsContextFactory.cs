using Microsoft.EntityFrameworkCore.Design;

namespace Hrms.adms.Insfrastructure;

file sealed class DesignTimeTenantProvider : ITenantProvider
{
    public Guid TenantId => Guid.Empty;
    public void SetTenantId(Guid tenantId) { }
}

public class AdmsContextFactory : IDesignTimeDbContextFactory<AdmsContext>
{
    private const string ApiUserSecretsId = "86b0bb2c-6312-466e-9a1f-b89ccbb70ac7";
    public AdmsContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ApiProjectDirectory())
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("HrmsConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Could not find 'HrmsConnection'. Set it in hrms-api/appsettings.Development.json, the hrms-api " +
                "user secrets (ConnectionStrings:HrmsConnection) or the ConnectionStrings__HrmsConnection env var.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AdmsContext>();
        var sqlVersion = new MySqlServerVersion(new Version(8, 4, 8));
        optionsBuilder.UseMySql(connectionString, sqlVersion);
        return new AdmsContext(optionsBuilder.Options,  new DesignTimeTenantProvider());
    }

    // The EF tools run from the startup or infrastructure project directory; either way the API's
    // settings sit in the sibling hrms-api folder.
    private static string ApiProjectDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var sibling = Path.GetFullPath(Path.Combine(currentDirectory, "..", "hrms-adms-api"));
        return Directory.Exists(sibling) ? sibling : Path.GetFullPath(Path.Combine(currentDirectory, "Hrms.Api"));
    }
}
