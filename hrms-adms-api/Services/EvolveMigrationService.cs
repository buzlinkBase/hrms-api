
using EvolveDb;
using MySqlConnector;

namespace Hrms.adms.Services;

public class EvolveMigrationService : IMigrationService
{
    public void Migrate(string? connectionString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return;
            using var connection = new MySqlConnection(connectionString);
            string migrationLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db", "migrations");
            var evolve = new Evolve(connection, msg => Log.Information(msg))
            {
                // Where your .sql files are located
                Locations = new[] { migrationLocation },
                // Don't allow Evolve to wipe data in Production
                IsEraseDisabled = true,
                // Name of the table that tracks history (instead of __EFMigrationsHistory)
                MetadataTableName = "changelog",
                // Wrap the whole migration in a transaction
                EnableClusterMode = false
            };
            evolve.Migrate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migration failed for {ConnectionString}", connectionString);
            throw;
        }
    }
}