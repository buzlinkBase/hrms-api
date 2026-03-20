namespace Hrms.adms.Extensions;

public static class RabbitMqConfiguration
{
    public static void RmqConfig(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>();
        if (settings == null) return;
        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<AdmsContext>(o =>
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
                    cb.TripThreshold = 15;
                    cb.ResetInterval = TimeSpan.FromMinutes(5);
                });
                cfg.UseConsumeFilter(typeof(TenantConsumeFilter<>), context);
                cfg.UsePublishFilter(typeof(TenantPublishFilter<>), context);
                cfg.Host(settings.Host, settings.VirtualHost, h =>
                {
                    h.Username(settings.Username);
                    h.Password(settings.Password);
                    //h.UseCluster(c => { /* If you have multiple RabbitMQ nodes */ });
                });
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
