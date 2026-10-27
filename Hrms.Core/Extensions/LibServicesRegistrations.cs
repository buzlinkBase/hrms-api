using Hrms.Core.Calculators.DtrPay;
using Hrms.Core.Services.AI;
using Hrms.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Hrms.Core.Extensions;

public static class LibServicesRegistrations
{
    public static void RegisterHRCoreServices(this IServiceCollection services)
    {
        AddLibraryAssemblyDependencies(services, "Hrms.Core");
        AddLibraryAssemblyDependencies(services, "DTR.Core");
        services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
        // Delegation: the approval engine reads it through this interface (see ApprovalEngineService).
        services.AddScoped<Services.Approvals.IApprovalDelegationLookup>(sp => sp.GetRequiredService<Services.Approvals.ApprovalDelegationService>());
        // My Approvals -- one adapter per employee request type; see IApprovalRequestAdapter.
        services.AddScoped<Services.Approvals.IApprovalRequestAdapter, Services.Approvals.LeaveApprovalAdapter>();
        services.AddScoped<Services.Approvals.IApprovalRequestAdapter, Services.Approvals.OvertimeApprovalAdapter>();
        services.AddScoped<Services.Approvals.IApprovalRequestAdapter, Services.Approvals.OfficialBusinessApprovalAdapter>();
        services.AddScoped<Services.Approvals.IApprovalRequestAdapter, Services.Approvals.PassSlipApprovalAdapter>();
        services.AddScoped<Services.Approvals.IApprovalRequestAdapter, Services.Approvals.LoanApprovalAdapter>();
        services.AddScoped<Services.Approvals.IApprovalRequestAdapter, Services.Approvals.ProfileUpdateApprovalAdapter>();
        services.AddScoped<IMigrationService, EvolveMigrationService>();
        services.AddScoped<EmployeePayrollInclusionResolver>();
        services.AddScoped<Messaging.InstanceProvisioner>();
        services.AddScoped<Messaging.DedicatedProvisioner>();
        AddAnnouncementAudience(services);
        // One per request; HrmsContext's query filter reads it (see ConfidentialAccessScope).
        services.AddScoped<Services.Security.ConfidentialAccessScope>();
        services.AddScoped<IConfidentialAccess>(sp => sp.GetRequiredService<Services.Security.ConfidentialAccessScope>());
        Messaging.DailyBenefits.DailyBenefitsRegistration.AddDailyBenefits(services);
        // Archive-then-delete of a locked company's data -- one ICompanyPurgeStep per link of the chain.
        Services.DataPurge.CompanyDataPurgeRegistration.AddCompanyDataPurge(services);
        // Subscription plan limits -- one counter per limit this service owns (see PlanLimitGuard).
        services.AddScoped<Services.Subscriptions.ILimitUsageCounter, Services.Subscriptions.ActiveEmployeeCounter>();
        services.AddScoped<Services.Subscriptions.IStorageQuotaGuard, Services.Subscriptions.StorageQuotaGuard>();
        // The Client Portal's HTTP ingest API (branding only; attendance goes over the bus). Address and key are per company, so they are set on each request.
        services.AddHttpClient<Services.ClientPortal.IClientPortalClient, Services.ClientPortal.ClientPortalClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
        // Sends the company's name and logo to the portal when they change (best effort; see ClientPortalBrandingService).
        services.AddScoped<Services.ClientPortal.IClientPortalBrandingPublisher, Services.ClientPortal.ClientPortalBrandingService>();
        // Record-attached files -- one handler per feature that can carry attachments (see AttachmentService).
        services.AddScoped<Services.Attachments.IAttachmentOwnerHandler, Services.Attachments.Handlers.EmployeeRecordAttachmentHandler>();
        services.AddScoped<Services.Attachments.IAttachmentOwnerHandler, Services.Attachments.Handlers.AssignAssetAttachmentHandler>();
        services.AddScoped<Services.Attachments.IAttachmentOwnerHandler, Services.Attachments.Handlers.LeaveApplicationAttachmentHandler>();
        services.AddScoped<Services.Attachments.IAttachmentOwnerHandler, Services.Attachments.Handlers.TravelOrderAttachmentHandler>();
        services.AddScoped<Services.Attachments.IAttachmentOwnerHandler, Services.Attachments.Handlers.PassSlipAttachmentHandler>();
        // Storage Manager -- one IStoredFileCategory per kind of stored file besides attachments
        // (each attachment handler becomes a category of its own); see StoredFileCategoryChain.
        services.AddScoped<Services.StorageManager.IStoredFileCategory, Services.StorageManager.EmployeePhotoCategory>();
        services.AddScoped<Services.StorageManager.IStoredFileCategory, Services.StorageManager.CompanyLogoCategory>();
        services.AddScoped<Services.StorageManager.IStoredFileCategory, Services.StorageManager.ClientLogoCategory>();
        services.AddScoped<Services.StorageManager.StoredFileCategoryChain>();
        services.AddScoped<Services.Subscriptions.IPlanLimitGuard,Services.Subscriptions.PlanLimitGuard>();
        services.AddScoped<Services.Subscriptions.IAttendanceSourceGuard, Services.Subscriptions.AttendanceSourceGuard>();
        // Payroll-run calculators are stateless (context flows through Calculate(), never
        // stored), so they're singletons instead of being `new`'d per employee per run.
        services.AddSingleton<ICalculator<AllowancePipeData, PayrollContext>, AllowancesCalculator>();
        services.AddSingleton<ICalculator<DeductionPipeData, DeductionPayloadContext>, DeductionCalculator>();
        services.AddSingleton<IDailyRateResolver, DailyRateResolver>();
        services.AddSingleton<IPipeLine<PayrollContext, AllowancePipeData>, AllowancePipeline>();
        services.AddSingleton<IPipeLine<DeductionPayloadContext, DeductionPipeData>, DeductionPipeline>();

        // Per-day DTR pay: one IDtrPayCalculator per OT/ND calculation method, picked by
        // IDtrPayCalculatorFactory -- see Calculators/DtrPay.
        services.AddDtrPayCalculators();
    }

    // One IAudienceRuleStrategy per way of targeting employees; AudienceResolver composes them.
    public static void AddAnnouncementAudience(IServiceCollection services)
    {
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.AllEmployeesAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.DepartmentAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.PositionAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.BranchAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.ProjectSiteAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.ClientAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.SectionAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.EmploymentStatusAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.IAudienceRuleStrategy, Services.Announcements.Audience.SpecificEmployeeAudienceRule>();
        services.AddSingleton<Services.Announcements.Audience.AudienceResolver>();
        services.AddScoped<Services.Announcements.Lifecycle.IAnnouncementJobScheduler, Services.Announcements.Lifecycle.QuartzAnnouncementJobScheduler>();
    }

    public static void AddLibraryAssemblyDependencies(IServiceCollection services, string assemblyName)
    {
        var libraryAssembly = Assembly.Load(assemblyName);
        var typesToRegister = libraryAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Service"));

        foreach (var type in typesToRegister)
        {
            var attr = type.GetCustomAttribute<ServiceRegistrationAttribute>();
            if (attr != null && attr.Exclude) continue;
            var lifetime = attr?.Lifetime ?? ServiceLifetime.Scoped;
            switch (lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddSingleton(type);
                    break;
                case ServiceLifetime.Transient:
                    services.AddTransient(type);
                    break;
                default:
                    services.AddScoped(type);
                    break;
            }
        }
    }
}


public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the generic AI extraction utility. Consumers inject
    /// IAiExtractionService; the Gemini implementation and schema generator
    /// are singletons (the SDK client is thread-safe and schemas are cached).
    /// </summary>
    public static IServiceCollection AddGeminiAiExtraction(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddSingleton<IAiSchemaGenerator, ReflectionAiSchemaGenerator>();
        services.AddSingleton<IAiExtractionService, GeminiAiExtractionService>();
        services.AddSingleton<IAiNotificationService, SignalRAiNotificationService>();
        return services;
    }
}
