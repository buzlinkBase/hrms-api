using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;

public class HrmsContextFactory : IDesignTimeDbContextFactory<HrmsContext>
{
    public HrmsContext CreateDbContext(string[] args)
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        string basePath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "hrms-api"));
        if (!Directory.Exists(basePath))
        {
            basePath = Path.GetFullPath(Path.Combine(currentDirectory, "Hrms.Api"));
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            //.AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("HrmsConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Could not find 'connection string'. Check your appsettings.json path.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<HrmsContext>();
        // Explicit, deterministic, and fast
        var sqlVersion = new MySqlServerVersion(new Version(8, 4, 8)); // or Version(9, 2, 0)
        optionsBuilder.UseMySql(connectionString, sqlVersion, x => x.UseNetTopologySuite());
        return new HrmsContext(optionsBuilder.Options, null!, null!, null!);

    }
}