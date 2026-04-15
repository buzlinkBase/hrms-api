
using MassTransit;
using Microsoft.Extensions.Hosting;

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
                cfg.Host(rabbitUri, h =>
                {
                    h.ConfigureOptions(options =>
                    {
                        options.AddressFamily = System.Net.Sockets.AddressFamily.InterNetwork;
                    });
                });
                cfg.SetQuorumQueue();
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}