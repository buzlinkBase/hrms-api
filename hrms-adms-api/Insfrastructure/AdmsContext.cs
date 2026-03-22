
namespace Hrms.adms.Insfrastructure;

public class AdmsContext : DbContext
{
    private readonly IConfiguration _configuration;
    private readonly TenantConnectionInfo _tci;
    public AdmsContext(DbContextOptions<AdmsContext> options):base(options)
    {
        
    }
    //public AdmsContext(
    //    DbContextOptions<AdmsContext> options,
    //    ITenantProvider tenantProvider,
    //    IConfiguration configuration,
    //    TenantConnectionInfo tci) : base(options)
    //{
    //    _configuration = configuration;
    //    _tci = tci;
    //}

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        //if (_tci == null || _tenantProvider == null) return;
        var connectionString = _tci.ConnectionString 
            ?? _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            optionsBuilder.UseLazyLoadingProxies(true);
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 45));
            optionsBuilder.UseMySql(connectionString, serverVersion);
            optionsBuilder.AddInterceptors(new SoftDeleteInterceptor());
        }
        base.OnConfiguring(optionsBuilder);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.UseDateFilter();
    }

    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
    public DbSet<Attendance>  Attendances { get; set; }

}
