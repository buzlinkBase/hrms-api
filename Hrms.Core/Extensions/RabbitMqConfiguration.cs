using Hrms.Core.Messaging;
using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Messaging.Filter;
using Hrms.Core.Messaging.LeaveWorkers;
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
            x.AddConsumer<CreateAttendanceWorker, AttendanceConsumerDefinition>();
            x.AddConsumer<TenantCreationCompletedWorker, TenantCreatedDefinition>();
            x.AddConsumer<AccountLinkedWorker, AccountLinkedDefinition>();
            x.AddConsumer<TenantInitConfigWorker, TenantInitDataWorkerDefination>();
            x.AddConsumer<DbMigrationActionWorker, DbMigrationActionWorkerDefinition>();
            x.AddConsumer<LeaveGrantOnEventWorker, LeaveGrantOnEventWorkerDefinition>();
            x.AddConsumer<LeaveAccrualWorker, LeaveAccrualWorkerDefinition>();
            x.AddConsumer<UniformAllowanceAccrualWorker, UniformAllowanceAccrualWorkerDefinition>();
            x.AddConsumer<LeavePeriodGrantWorker, LeavePeriodGrantWorkerDefinition>();
            x.AddConsumer<LeaveCarryOverWorker, LeaveCarryOverWorkerDefinition>();
            x.AddConsumer<UserOnboardedWorker, UserOnboardedWorkerDefinition>();
            x.AddEntityFrameworkOutbox<HrmsContext>(o =>
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
public class AttendanceConsumerDefinition : ConsumerDefinition<CreateAttendanceWorker>
{
    public AttendanceConsumerDefinition()
    {
        EndpointName = "hrms-attendance-created-que";
    }
}
public class TenantCreatedDefinition : ConsumerDefinition<TenantCreationCompletedWorker>
{
    public TenantCreatedDefinition()
    {
        EndpointName = "hrms-tenant-created-que";
    }
}
public class AccountLinkedDefinition : ConsumerDefinition<AccountLinkedWorker>
{
    public AccountLinkedDefinition()
    {
        EndpointName = "hrms-account-linked-que";
    }
}
public class DbMigrationActionWorkerDefinition : ConsumerDefinition<DbMigrationActionWorker>
{
    public DbMigrationActionWorkerDefinition()
    {
        // ConcurrencyLimit ensures MassTransit processes them concurrently 
        // up to this limit. Keep this matching or lower than PrefetchCount.
        ConcurrentMessageLimit = 2;
        EndpointName = "hrms-migration-runner-que";
    }
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<DbMigrationActionWorker> consumerConfigurator,
        IRegistrationContext context)
    {
        // CRITICAL: Controls how many migration messages this worker pulls at once
        endpointConfigurator.PrefetchCount = 2;
    }
}

public class TenantInitDataWorkerDefination : ConsumerDefinition<TenantInitConfigWorker>
{
    public TenantInitDataWorkerDefination()
    {
        EndpointName = "hrms-tenant-default-config-que";
    }
}

public class UserOnboardedWorkerDefinition : ConsumerDefinition<UserOnboardedWorker>
{
    public UserOnboardedWorkerDefinition()
    {
        EndpointName = "hrms-user-onboarded-que";
    }
}
