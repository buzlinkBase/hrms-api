using Microsoft.EntityFrameworkCore.Design;

namespace Hrms.adms.Insfrastructure;

public class AdmsContextFactory : IDesignTimeDbContextFactory<AdmsContext>
{
    public AdmsContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AdmsContext>();
        var serverVersion = new MySqlServerVersion(new Version(9, 2, 0));
        var connectionString = "server=127.0.0.1;port=3316;database=adms;user=oneuser;pwd=Pokemon67584321";
        optionsBuilder.UseMySql(connectionString, serverVersion);
        //return new AdmsContext(optionsBuilder.Options, null, null, null);
        return new AdmsContext(optionsBuilder.Options);
    }
}
