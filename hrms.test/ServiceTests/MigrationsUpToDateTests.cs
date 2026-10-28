using Hrms.adms.Insfrastructure;
using Hrms.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// A table or field change must ship with its migration: until it does, the live database doesn't have
/// it, and the purge and the console's permanent deletion -- which read the EF model -- would run
/// statements against tables or columns that aren't there. Built the way the EF tools build the
/// contexts (MySQL 8.4); no database is contacted.
/// </summary>
public class MigrationsUpToDateTests
{
    private const string NoDatabase = "server=localhost;port=1;database=none;user=none;pwd=none";
    private static readonly MySqlServerVersion Version = new(new Version(8, 4, 8));

    [Fact]
    public void TheHrModel_HasNoChangesWithoutAMigration()
    {
        var options = new DbContextOptionsBuilder<HrmsContext>()
            .UseMySql(NoDatabase, Version, x => x.UseNetTopologySuite())
            .Options;
        using var db = new HrmsContext(options, null!, null!, null!);

        db.Database.HasPendingModelChanges().Should().BeFalse(
            "the HrmsContext model changed without a migration -- add one (dotnet ef migrations add) before it ships");
    }

    [Fact]
    public void TheDeviceModel_HasNoChangesWithoutAMigration()
    {
        var options = new DbContextOptionsBuilder<AdmsContext>()
            .UseMySql(NoDatabase, Version)
            .Options;
        using var db = new AdmsContext(options, Substitute.For<ITenantProvider>());

        db.Database.HasPendingModelChanges().Should().BeFalse(
            "the AdmsContext model changed without a migration -- add one (dotnet ef migrations add) before it ships");
    }
}
