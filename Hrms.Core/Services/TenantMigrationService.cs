//using hrms_api.Data;
//using Microsoft.EntityFrameworkCore;

//namespace Hrms.Core.Services;

//public class Tenant
//{
//    public Guid Id { get; set; }
//    public Guid TenantId { get; set; }
//    public string ConnectionString { get; set; }
//}

//public interface ITenantRegistry
//{
//    public Task<List<Tenant>> GetActiveTenantsAsync();
//}


//public class TenantRegistry : ITenantRegistry
//{
//    public Task<List<Tenant>> GetActiveTenantsAsync()
//    {
//        return Task.FromResult(new List<Tenant>()
//       {
//           new Tenant(){
//               TenantId = Guid.Empty,
//               ConnectionString="server=localhost;port=3307;database=hr;user=root;pwd=Pokemon67584321@#$"}
//       });
//    }
//}
//public class TenantMigrationService
//{
//    private readonly ILogger<TenantMigrationService> _logger;
//    private readonly ITenantProvider _tenantProvider;
//    private readonly ITenantRegistry _registry;

//    public TenantMigrationService(ITenantProvider tenantProvider, 
//        ITenantRegistry registry,
//        ILogger<TenantMigrationService> logger)
//    {
//        _tenantProvider = tenantProvider;
//        _registry = registry;
//        _logger = logger;
//    }
//    public async Task RunMigrationsAsync()
//    {
//        var tenants = await _registry.GetActiveTenantsAsync();
//        foreach (var tenant in tenants)
//        {
//            try
//            {
//                var connectionString = tenant.ConnectionString;
//                var options = new DbContextOptionsBuilder<HRMSDbContext>()
//                     .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
//                    .Options;

//                using var context = new HRMSDbContext(options, _tenantProvider);
//                await context.Database.MigrateAsync(); // EF Core migration
//                _logger.LogInformation($"Migration successful for {_tenantProvider.TenantId}");
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, $"Migration failed for {_tenantProvider.TenantId}");
//                // Optionally: mark tenant as needing manual review
//            }
//        }
//    }
//}