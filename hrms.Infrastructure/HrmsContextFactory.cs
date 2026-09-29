using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;

/// <summary>
/// Used only by the EF tools (dotnet ef migrations / database update). Reads the connection string
/// the same places the API does, never from the committed appsettings.json, so no real database
/// credentials have to live in the repo:
///   1. hrms-api/appsettings.Development.json (local database)
///   2. the hrms-api project's user secrets    (dotnet user-secrets --project hrms-api ...)
///   3. environment variables                  (ConnectionStrings__HrmsConnection)
/// Later sources win, so pointing the tools at another database is a secret or an env var, not a
/// code change.
/// </summary>
public class HrmsContextFactory : IDesignTimeDbContextFactory<HrmsContext>
{
    // hrms-api.csproj's <UserSecretsId>. Infrastructure can't reference the API project (the API
    // references Infrastructure), so its secrets are loaded by id rather than via typeof(Program).
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

    // The EF tools run from the startup or infrastructure project directory; either way the API's
    // settings sit in the sibling hrms-api folder.
    private static string ApiProjectDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var sibling = Path.GetFullPath(Path.Combine(currentDirectory, "..", "hrms-api"));
        return Directory.Exists(sibling) ? sibling : Path.GetFullPath(Path.Combine(currentDirectory, "Hrms.Api"));
    }
}
