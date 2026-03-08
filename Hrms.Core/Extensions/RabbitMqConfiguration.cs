using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using OnePunch.Auth.Core.Messaging;

namespace Hrms.Core.Extensions;

public static class  RabbitMqConfiguration
{
    public static void  HrmsConfigRabbitMq(this WebApplicationBuilder builder)
    {
        // 1. Only fetch what you actually need
        var settings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>();
        if (settings == null) return;

        builder.Services.AddMassTransit(x =>
        {
            // 2. Register Consumers
            x.AddConsumer<BranchWorker>();
            x.AddEntityFrameworkOutbox<HrmsContext>(o =>
            {
                o.UseMySql();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(5);
                o.DisableInboxCleanupService();
                //o.EnableInboxCleanupService();
            });

            // 4. Configure RabbitMQ Transport
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.UseMessageRetry(r =>
                {
                    r.Exponential(5, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
                });

                // Circuit Breaker prevents slamming a failing service
                cfg.UseCircuitBreaker(cb =>
                {
                    cb.TrackingPeriod = TimeSpan.FromMinutes(1);
                    cb.TripThreshold = 15; // Trip after 15 failures
                    cb.ResetInterval = TimeSpan.FromMinutes(5); // Wait 5 mins before trying again
                });

                cfg.UsePublishFilter(typeof(TenantPublishFilter<>), context);
                cfg.UseConsumeFilter(typeof(TenantConsumeFilter<>), context);
                cfg.ConfigureEndpoints(context);


                cfg.Host(settings.Host, settings.VirtualHost, h =>
                {
                    h.Username(settings.Username);
                    h.Password(settings.Password);
                    //h.UseCluster(c => { /* If you have multiple RabbitMQ nodes */ });
                });

                //cfg.ReceiveEndpoint("hrms-branch-created", e =>
                //{
                //    e.ConfigureConsumer<BranchWorker>(context);
                //    e.Durable = true;
                //    e.AutoDelete = false; // Never auto-delete your durable queues
                //    // Crucial: Use a Dead Letter Exchange (DLX) for failed messages
                //    //e.ConfigureDeadLetterQueue();
                //    e.ConfigureConsumer<BranchWorker>(context);
                //});
                // This handles any consumers not explicitly mapped above
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}