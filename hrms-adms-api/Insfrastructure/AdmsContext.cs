
namespace Hrms.adms.Insfrastructure;

public class AdmsContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;
    public AdmsContext(DbContextOptions<AdmsContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    //public AdmsContext(DbContextOptions<AdmsContext> options):base(options) 
    //{
    //}
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        modelBuilder.UseTenantAndDateFilter(_tenantProvider.TenantId);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.Entity<BiometricDetail>(entity =>
        {
            entity.ToTable("BiometricDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>();
        });

        // 2. Fix the Biometrics relationship mapping
        modelBuilder.Entity<Biometrics>(entity =>
        {
            entity.ToTable("Biometrics");
            entity.HasKey(e => e.Id);
            entity.Ignore(b => b.Fingerprint);
            entity.Ignore(b => b.Face);
            entity.Ignore(b => b.FingerVein);
            entity.Ignore(b => b.PalmVein);
        });
    }

    public DbSet<DeviceCommand> DeviceCommands { get; set; }
    public DbSet<BiometricDevice> BiometricDevices { get; set; }
    public DbSet<SystemCounters> SystemCounters { get; set; }
    public DbSet<BiometricDetail> BiometricDetails { get; set; }
    public DbSet<Biometrics> Biometrics { get; set; }
    public DbSet<PhotosAndMedia> PhotosAndMedias { get; set; }
    public DbSet<FeaturesAndProtocols> FeaturesAndProtocols { get; set; }
    public DbSet<QrCodeConfig> QrCodeConfigs { get; set; }
    public DbSet<ThermalAndMaskConfig> ThermalAndMaskConfigs { get; set; }
    public DbSet<MultiBioSupport> MultiBioSupports { get; set; }
    public DbSet<BiometricTemplate> BiometricTemplates { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<DeviceUser> DeviceUsers { get; set; }
}
