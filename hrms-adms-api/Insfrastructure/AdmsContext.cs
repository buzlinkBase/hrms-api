namespace Hrms.adms.Insfrastructure;
public class AdmsContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;
    private readonly TenantConnectionInfo _tci;
    public AdmsContext(
        DbContextOptions<AdmsContext> options,
        ITenantProvider tenantProvider,
        IConfiguration configuration,
        TenantConnectionInfo  tci) : base(options)
    {
        _tenantProvider = tenantProvider;
        _configuration = configuration;
        _tci = tci;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        if (_tci == null || _tenantProvider == null) return;
        var connectionString = _tci.ConnectionString ?? _configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            optionsBuilder.AddInterceptors(
                new ApplyTenantInterceptor(_tenantProvider),
                new SoftDeleteInterceptor()
            );
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
        if (_tenantProvider != null && _tenantProvider.TenantId != Guid.Empty)
        {
            modelBuilder.UseDateFilter();
        }
    }

    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
}
