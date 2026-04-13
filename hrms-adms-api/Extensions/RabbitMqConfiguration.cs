using Hrms.Core.Messaging;
namespace Hrms.adms.Extensions;
public static class RabbitMqConfiguration
{
    public static void AdmsConfigRabbitMq(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>();
        if (settings == null) return;

        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer<TenantCreatedWorker, TenantCreatedDefinition>();
            x.AddConsumer<DbMigrationActionWorker, DbMigrationActionWorkerDefinition>();
            x.AddConsumer<AttSyncResponseWorker, AttSyncWorkerDefinition>();
            x.AddEntityFrameworkOutbox<AdmsContext>(o =>
            {
                o.UseMySql();
                o.UseBusOutbox();
            });
            x.SetEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance);
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.UseMessageRetry(r =>
                {
                    r.Exponential(5, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
                });
                cfg.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                    cb.TripThreshold = 15; // Trip after 15 failures
                    cb.ResetInterval = TimeSpan.FromMinutes(5); // Wait 5 mins before trying again
                });
                cfg.UseConsumeFilter(typeof(TenantConsumeFilter<>), context);
                cfg.UsePublishFilter(typeof(TenantPublishFilter<>), context);
                cfg.Host(settings.Uri);
                cfg.SetQuorumQueue();
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}

public class TenantCreatedDefinition : ConsumerDefinition<TenantCreatedWorker>
{
    public TenantCreatedDefinition()
    {
        EndpointName = "adms-tenant-created-que";
    }
}
public class DbMigrationActionWorkerDefinition : ConsumerDefinition<DbMigrationActionWorker>
{
    public DbMigrationActionWorkerDefinition()
    {
        EndpointName = "adms-migration-runner-que";
    }
}

public class AttSyncWorkerDefinition : ConsumerDefinition<AttSyncResponseWorker>
{
    public AttSyncWorkerDefinition()
    {
        EndpointName = "adms-attendance-sync-response-que";
    }
}
