using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace Hrms.Infrastructure;

public class HrmsContextFactory : IDesignTimeDbContextFactory<HrmsContext>
{
    public HrmsContext CreateDbContext(string[] args)
    {
        //// 1. Get the path to Hrms.Api properly
        //string currentDirectory = Directory.GetCurrentDirectory();
        //string basePath = Path.GetFullPath(Path.Combine(currentDirectory, "..", "Hrms.Api"));
        //if (!Directory.Exists(basePath))
        //{
        //    basePath = Path.GetFullPath(Path.Combine(currentDirectory, "Hrms.Api"));
        //}

        //IConfigurationRoot configuration = new ConfigurationBuilder()
        //    .SetBasePath(basePath)
        //    //.AddJsonFile("appsettings.json", optional: false) // Essential config
        //    .AddJsonFile("appsettings.Development.json", optional: true)
        //    .Build();

        //var connectionString = configuration.GetConnectionString("DefaultConnection");

        //if (string.IsNullOrEmpty(connectionString))
        //{
        //    throw new InvalidOperationException("Could not find 'DefaultConnection'. Check your appsettings.json path.");
        //}

        var optionsBuilder = new DbContextOptionsBuilder<HrmsContext>();
        var serverVersion = new MySqlServerVersion(new Version(9, 2, 0));
        var connectionString = "server=127.0.0.1;port=3316;database=hrms;user=oneuser;pwd=Pokemon67584321";
        optionsBuilder.UseMySql(connectionString, serverVersion);
        return new HrmsContext(optionsBuilder.Options,null,null,null);
    }
}