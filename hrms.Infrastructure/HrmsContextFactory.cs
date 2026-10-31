using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;
public class HrmsContextFactory : IDesignTimeDbContextFactory<HrmsContext>
{
    private const string ApiUserSecretsId = "86b0bb2c-6312-466e-9a1f-b89ccbb70ac6";
    public HrmsContext CreateDbContext(string[] args)
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

        var optionsBuilder = new DbContextOptionsBuilder<HrmsContext>();
        var sqlVersion = new MySqlServerVersion(new Version(8, 4, 8));
        optionsBuilder.UseMySql(connectionString, sqlVersion, x => x.UseNetTopologySuite());
        return new HrmsContext(optionsBuilder.Options, null!, null!, null!);
    }
    private static string ApiProjectDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var sibling = Path.GetFullPath(Path.Combine(currentDirectory, "..", "hrms-api"));
        return Directory.Exists(sibling) ? sibling : Path.GetFullPath(Path.Combine(currentDirectory, "Hrms.Api"));
    }
}
