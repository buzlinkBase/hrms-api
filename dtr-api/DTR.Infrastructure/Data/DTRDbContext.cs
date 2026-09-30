
using Microsoft.Extensions.Configuration;
using Onepunch.Common.Lib;
using Onepunch.Common.Lib.Entities;

namespace DTR.Infrastructure;
public class DTRDbContext : DbContext
{
    public readonly Guid TenantId = Guid.Empty;
    public DTRDbContext(DbContextOptions options, Guid tenantId) : base(options)
    {
        TenantId = tenantId;
    }
    public DTRDbContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.EnableSensitiveDataLogging();
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        if (TenantId != Guid.Empty) modelBuilder.UseSoftDelete(TenantId);

        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.TenantId);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.ProcessedOn);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.Status);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.RetryCount);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.NextRetryOn);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.Topic);
        modelBuilder.Entity<OutboxMessage>().HasIndex(x => x.Key);
        modelBuilder.Entity<OutboxMessage>()
           .Property(x => x.Status)
            .HasConversion(
                  v => v.ToString(),
                  v => EnumParserConfig.SafeParseEnum(v, OutBoxState.PENDING)
              );
    } 

    public DbSet<Employee> Employees { get; set; }
    public DbSet<DailyRecord> DailyRecords { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<ManualBatchEntryLogEntity> ManualBatchEntryLogs { get; set; }
    public DbSet<UnkownEmpAttendance> UnknownAtt { get; set; }
    public DbSet<TimeShift> TimeShifts { get; set; }
    public DbSet<WorkSchedulePlan> WorkSchedulePlans { get; set; }
    //public DbSet<Department> Departments { get; set; }
    //public DbSet<OperationArea> OperationAreas { get; set; }
    //public DbSet<PayrollGroup> PayrollGroup { get; set; }
    //public DbSet<Client> Clients { get; set; }
    //public DbSet<ClientHoliday> ClientHolidays { get; set; }
    public DbSet<ChangeHoliday> ChangeHolidays { get; set; }
    public DbSet<ChangeRestDay> ChangeRestDays { get; set; }
    public DbSet<RestDay> DayOffs { get; set; }
    public DbSet<RestDayDate> RestDayDates { get; set; }
    public DbSet<Holiday> Holidays { get; set; }
    public DbSet<LogLimit> LogLimits { get; set; }
    public DbSet<LeaveApplication> LeaveApplications { get; set; }
    public DbSet<LeaveApplicationDetail> LeaveApplicationDetails { get; set; }
    public DbSet<OverTimeApplicationEntity> OverTimeApplications { get; set; }
    public DbSet<UnderTimeApplication> UnderTimeApplications { get; set; }
    public DbSet<DailyRecordPunchCounter> DTRPunchCounters { get; set; }
    public DbSet<GeneralSetting> GeneralSettings { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

}

/// <summary>
/// Used only by the EF tools (migrations / database update). Reads the connection string the
/// same places the API does -- never from source or the committed appsettings.json:
///   1. DTR.API/appsettings.Development.json (local database)
///   2. the API project's user secrets (dotnet user-secrets --project DTR.API/DTR.API ...)
///   3. environment variables (ConnectionStrings__dtr)
/// Later sources win.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<DTRDbContext>
{
    // DTR.API.csproj's <UserSecretsId>. Infrastructure can't reference the API project (the API
    // references Infrastructure), so its secrets are loaded by id.
    private const string ApiUserSecretsId = "b8e3d5f1-2a64-4c9b-8e17-6d0f4a2c9b53";

    public DTRDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ApiProjectDirectory())
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(ApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();

        var constr = configuration.GetConnectionString("dtr");
        if (string.IsNullOrEmpty(constr))
        {
            throw new InvalidOperationException(
                "Could not find the 'dtr' connection string. Set it in DTR.API/appsettings.Development.json, the API's " +
                "user secrets (ConnectionStrings:dtr) or the ConnectionStrings__dtr env var.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<DTRDbContext>();
        optionsBuilder.UseMySql(constr, ServerVersion.AutoDetect(constr));
        return new DTRDbContext(optionsBuilder.Options);
    }

    // The EF tools run from this project's or the API project's directory; the API's settings
    // live in dtr-api/DTR.API/DTR.API.
    private static string ApiProjectDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        foreach (var candidate in new[]
                 {
                     Path.Combine(currentDirectory, "..", "DTR.API", "DTR.API"),
                     Path.Combine(currentDirectory, "DTR.API", "DTR.API"),
                 })
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full)) return full;
        }
        return currentDirectory;
    }
}