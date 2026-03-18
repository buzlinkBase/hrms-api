namespace Hrms.adms.Insfrastructure;
public class AdmsContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly TenantConnectionInfo _tci;
    public AdmsContext(
        DbContextOptions<AdmsContext> options,
        ITenantProvider tenantProvider,
        TenantConnectionInfo  tci) : base(options)
    {
        _tenantProvider = tenantProvider;
        _tci = tci;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        if (optionsBuilder.IsConfigured) return;
        if (_tci == null || _tenantProvider == null) return;
        var connectionString = _tci.ConnectionString;
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
            modelBuilder.UseDateFilter();
        }
    }

    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
}
