using Microsoft.EntityFrameworkCore; 
namespace Hrms.adms.Insfrastructure;
public class AdmsContext  : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IConnectionStringProvider _conProvider;
    public AdmsContext(
        DbContextOptions<AdmsContext> options,
        ITenantProvider tenantProvider,
        IConnectionStringProvider conProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
        _conProvider = conProvider;
    }


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        if (_conProvider == null || _tenantProvider == null) return;
        var connectionString = _conProvider.GetConnectionString(_tenantProvider.TenantId);
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
            modelBuilder.UseSoftDelete(_tenantProvider.TenantId);
        }
    }
 
    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
}
