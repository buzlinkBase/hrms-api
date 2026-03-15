namespace Hrms.adms.Insfrastructure;

public class AdmsContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly TenantConnectionInfo _tenantConnectionInfo;
    public AdmsContext(
        DbContextOptions<AdmsContext> options,
        ITenantProvider tenantProvider,
        TenantConnectionInfo  tenantConnectionInfo) : base(options)
    {
        _tenantProvider = tenantProvider;
        _tenantConnectionInfo = tenantConnectionInfo;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (optionsBuilder.IsConfigured) return;
        if (_tenantConnectionInfo == null || _tenantProvider == null) return;
        var connectionString = _tenantConnectionInfo.ConnectionString;
        if (!string.IsNullOrEmpty(connectionString))
        {
            optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            optionsBuilder.AddInterceptors(
                new ApplyTenantInterceptor(_tenantProvider),
                new SoftDeleteInterceptor()
            );
        }
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
            modelBuilder.UseSoftDelete(_tenantProvider.TenantId);
        }
    }

    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
}
