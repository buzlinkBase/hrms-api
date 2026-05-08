
using MassTransit;
using Microsoft.Extensions.Hosting;
using Onepunch.Common.Lib;

namespace MigrationHrns;

public static class RabbitMqConfiguration
{
    public static void rmqConfig(this HostApplicationBuilder builder)
    {
        var rmqUri = builder.Configuration["RabbitMqSettings:Uri"];
        if (string.IsNullOrWhiteSpace(rmqUri)) return;
        builder.Services.AddMassTransit(x =>
        {
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
                //cfg.Host(host, virtualHost, h =>
                //{
                //    h.Username(username);
                //    h.Password(password);
                //});
                var rabbitUri = new Uri(rmqUri);
                cfg.Host(rabbitUri);
                cfg.SetQuorumQueue();
                cfg.ConfigureEndpoints(context);
            });
        });
    }
    //public static void HrmsConfigRabbitMq(this WebApplicationBuilder builder)
    //{
    //    var settings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>();
    //    if (settings == null) return;

    //    builder.Services.AddMassTransit(x =>
    //    {
    //        //x.AddConsumer<BranchWorker, BranchCreatedConsumerDefinition>();
    //        x.AddConsumer<CreateAttendanceWorker, AttendanceConsumerDefinition>();
    //        x.AddConsumer<TenantCreatedWorker, TenantCreatedDefinition>();
    //        x.AddConsumer<DbMigrationActionWorker, DbMigrationActionWorkerDefinition>();
    //        x.AddConsumer<TenantInitConfigWorker, TenantInitDataWorkerDefination>();
    //        x.AddEntityFrameworkOutbox<HrmsContext>(o =>
    //        {
    //            o.UseMySql();
    //            o.UseBusOutbox();
    //            //o.QueryDelay = TimeSpan.FromSeconds(5);
    //            //o.DisableInboxCleanupService();
    //            //o.EnableInboxCleanupService(); 
    //        });
    //        x.SetEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance);
    //        x.UsingRabbitMq((context, cfg) =>
    //        {
    //            cfg.UseMessageRetry(r =>
    //            {
    //                r.Exponential(5, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10));
    //            });
    //            cfg.UseCircuitBreaker(cb =>
    //            {
    //                cb.TrackingPeriod = TimeSpan.FromMinutes(1);
    //                cb.TripThreshold = 15; // Trip after 15 failures
    //                cb.ResetInterval = TimeSpan.FromMinutes(5); // Wait 5 mins before trying again
    //            });
    //            //cfg.Host(settings.Host, settings.VirtualHost, h =>
    //            //{
    //            //    h.Username(settings.Username);
    //            //    h.Password(settings.Password);
    //            //});
    //            var uri = new Uri(settings.Uri.Trim());
    //            cfg.Host(uri);
    //            cfg.UseConsumeFilter(typeof(TenantConsumeFilter<>), context);
    //            cfg.UsePublishFilter(typeof(TenantPublishFilter<>), context);
    //            cfg.SetQuorumQueue();
    //            cfg.ConfigureEndpoints(context);
    //        });
    //    });
    //}
}