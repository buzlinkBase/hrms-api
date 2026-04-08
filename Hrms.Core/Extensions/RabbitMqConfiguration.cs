using Hrms.Core.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using OnePunch.Auth.Core.Messaging;

namespace Hrms.Core.Extensions;

public static class RabbitMqConfiguration
{
    public static void HrmsConfigRabbitMq(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>();
        if (settings == null) return;

        builder.Services.AddMassTransit(x =>
        {
            //x.AddConsumer<BranchWorker, BranchCreatedConsumerDefinition>();
            x.AddConsumer<CreateAttendanceWorker, AttendanceConsumerDefinition>();
            x.AddConsumer<TenantCreatedWorker, TenantCreatedDefinition>();
            x.AddConsumer<DbMigrationActionWorker, DbMigrationActionWorkerDefinition>();
            x.AddConsumer<TenantInitConfigWorker, TenantInitDataWorkerDefination>();
            x.AddEntityFrameworkOutbox<HrmsContext>(o =>
            {
                o.UseMySql();
                o.UseBusOutbox();
                //o.QueryDelay = TimeSpan.FromSeconds(5);
                //o.DisableInboxCleanupService();
                //o.EnableInboxCleanupService(); 
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
                //cfg.Host(settings.Host, settings.VirtualHost, h =>
                //{
                //    h.Username(settings.Username);
                //    h.Password(settings.Password);
                //});
                cfg.Host("amqps://lriumdis:PNHXZ9uy2nWDyLQC4yJQEtN5H8zMRUSm@armadillo.rmq.cloudamqp.com/lriumdis");
                cfg.SetQuorumQueue();
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}

//public class BranchCreatedConsumerDefinition : ConsumerDefinition<BranchWorker>
//{
//    public BranchCreatedConsumerDefinition()
//    {
//        EndpointName = "hrms-branch-created-que";
//    }
//}
public class AttendanceConsumerDefinition : ConsumerDefinition<CreateAttendanceWorker>
{
    public AttendanceConsumerDefinition()
    {
        EndpointName = "hrms-attendance-created-que";
    }
}
public class TenantCreatedDefinition : ConsumerDefinition<TenantCreatedWorker>
{
    public TenantCreatedDefinition()
    {
        EndpointName = "hrms-tenant-created-que";
    }
}
public class DbMigrationActionWorkerDefinition : ConsumerDefinition<DbMigrationActionWorker>
{
    public DbMigrationActionWorkerDefinition()
    {
        EndpointName = "hrms-migration-runner-que";
    }
}

public class TenantInitDataWorkerDefination : ConsumerDefinition<TenantInitConfigWorker>
{
    public TenantInitDataWorkerDefination()
    {
        EndpointName = "hrms-tenant-default-config-que";
    }
}
