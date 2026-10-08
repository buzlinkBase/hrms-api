using Hrms.Core.Messaging;
using Hrms.Core.Messaging.Announcements;
using Hrms.Core.Messaging.DailyBenefits;
using Hrms.Core.Messaging.BenefitWorkers;
using Hrms.Core.Messaging.Filter;
using Hrms.Core.Messaging.LeaveWorkers;
using Hrms.Core.Messaging.Notifications;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using OnePunch.Auth.Core.Messaging;
using Quartz;

namespace Hrms.Core.Extensions;

public static class RabbitMqConfiguration
{
    public static void HrmsConfigRabbitMq(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("RabbitMqSettings").Get<RabbitMqSettings>();
        if (settings == null) return;

        builder.AddHrmsQuartz();

        builder.Services.AddMassTransit(x =>
        {
            // Future-dated messages (e.g. an announcement's auto-publish) are held by Quartz -- see
            // AddHrmsQuartz -- then published at their due time.
            x.AddPublishMessageScheduler();
            x.AddQuartzConsumers();
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
            // hrms-api self-consumes ApprovalNotificationRequested (the same message it
            // publishes from ApprovalEngineService) to push via SignalR -- see
            // ApprovalPushNotificationWorker's own doc comment.
            x.AddConsumer<ApprovalPushNotificationWorker, ApprovalPushNotificationWorkerDefinition>();
            x.AddConsumer<ApprovalPoolPushNotificationWorker, ApprovalPoolPushNotificationWorkerDefinition>();
            // Read copy of AuthApi/TenantApi audit events for Security > Audit -- see AuditReplicaWorker.
            x.AddConsumer<AuditReplicaWorker, AuditReplicaWorkerDefinition>();
            // Auto-publish / auto-archive / purge of announcements -- see AnnouncementLifecycleScheduler.
            x.AddConsumer<AnnouncementLifecycleWorker, AnnouncementLifecycleWorkerDefinition>();
            // One company's daily benefits steps, in order -- queued by the Quartz DailyBenefitsJob.
            x.AddConsumer<DailyBenefitsWorker, DailyBenefitsWorkerDefinition>();
            // Calculate DTR "Save Draft" for a whole cutoff, outside the HTTP request -- see DtrDraftSaveService.
            x.AddConsumer<SaveDtrDraftWorker, SaveDtrDraftWorkerDefinition>();
            // Evicts cached entitlements when an account's subscription changes.
            x.AddConsumer<Messaging.Subscriptions.SubscriptionChangedWorker, Messaging.Subscriptions.SubscriptionChangedWorkerDefinition>();
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
                cfg.UsePublishMessageScheduler();
                cfg.UseConsumeFilter(typeof(TenantConsumeFilter<>), context);
                cfg.UsePublishFilter(typeof(TenantPublishFilter<>), context);
                cfg.Host(settings.Uri);
                cfg.SetQuorumQueue();
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
public static class QuartzConfiguration
{
    /// <summary>
    /// Quartz as MassTransit's message scheduler, with a persistent job store in the shared HRIS
    /// database (ConnectionStrings:Quartz, else HrmsConnection) so scheduled messages survive
    /// restarts and broker loss. Clustered: with several API instances each job fires on exactly
    /// one of them. Needs Quartz's standard QRTZ_ tables (tables_mysql_innodb.sql for Quartz.NET
    /// 3.x) in that database.
    /// </summary>
    public static void AddHrmsQuartz(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Quartz")
                               ?? builder.Configuration.GetConnectionString("HrmsConnection")
                               ?? throw new InvalidOperationException("No connection string for the Quartz job store.");

        builder.Services.AddQuartz(q =>
        {
            q.SchedulerName = "hrms-scheduler";
            q.SchedulerId = "AUTO";
            q.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.UseMySqlConnector(db =>
                {
                    db.ConnectionString = connectionString;
                    db.TablePrefix = "QRTZ_";
                });
                store.UseSystemTextJsonSerializer();
                store.UseClustering();
            });

            // Daily benefits run (leave carry-over/grant/accrual, uniform allowance). Hour of day
            // in UTC -- the old LeaveScheduler:RunAtHour setting, default midnight. A fire missed
            // while every instance was down runs once as soon as one is back, and the job itself
            // catches up any days it missed (see DailyBenefitsJob).
            var runAtHour = Math.Clamp(builder.Configuration.GetValue<int?>("LeaveScheduler:RunAtHour") ?? 0, 0, 23);
            q.AddJob<DailyBenefitsJob>(DailyBenefitsJob.Key, job => job.StoreDurably());
            q.AddTrigger(trigger => trigger
                .ForJob(DailyBenefitsJob.Key)
                .WithIdentity(DailyBenefitsJob.TriggerKey)
                .WithCronSchedule($"0 0 {runAtHour} * * ?", cron => cron
                    .InTimeZone(TimeZoneInfo.Utc)
                    .WithMisfireHandlingInstructionFireAndProceed()));

            // Old notification-bell entries (Notifications:RetentionDays), daily at 02:00 UTC.
            q.AddJob<NotificationRetentionJob>(NotificationRetentionJob.Key, job => job.StoreDurably());
            q.AddTrigger(trigger => trigger
                .ForJob(NotificationRetentionJob.Key)
                .WithIdentity(NotificationRetentionJob.TriggerKey)
                .WithCronSchedule("0 0 2 * * ?", cron => cron
                    .InTimeZone(TimeZoneInfo.Utc)
                    .WithMisfireHandlingInstructionFireAndProceed()));
        });
        builder.Services.AddQuartzHostedService(options =>
        {
            options.StartDelay = TimeSpan.FromSeconds(5);
            options.WaitForJobsToComplete = true;
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

public class ApprovalPushNotificationWorkerDefinition : ConsumerDefinition<ApprovalPushNotificationWorker>
{
    public ApprovalPushNotificationWorkerDefinition()
    {
        EndpointName = "hrms-approval-push-notification-que";
    }
}

public class ApprovalPoolPushNotificationWorkerDefinition : ConsumerDefinition<ApprovalPoolPushNotificationWorker>
{
    public ApprovalPoolPushNotificationWorkerDefinition()
    {
        EndpointName = "hrms-approval-pool-push-notification-que";
    }
}

public class AnnouncementLifecycleWorkerDefinition : ConsumerDefinition<AnnouncementLifecycleWorker>
{
    public AnnouncementLifecycleWorkerDefinition()
    {
        EndpointName = "hrms-announcement-lifecycle-que";
    }
}

public class AuditReplicaWorkerDefinition : ConsumerDefinition<AuditReplicaWorker>
{
    public AuditReplicaWorkerDefinition()
    {
        EndpointName = "hrms-audit-replica-que";
    }
}
