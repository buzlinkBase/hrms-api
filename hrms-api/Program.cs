using Asp.Versioning.ApiExplorer;
using Hrms.adms.Middlewares;
using Hrms.Api.Exceptions;
using Hrms.Api.Extensions;
using Hrms.Core.Extensions;
using Hrms.Core.Hubs;
using Mapster;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        Log.Logger = new LoggerConfiguration()
       .ReadFrom.Configuration(builder.Configuration)
       .CreateLogger();
        builder.Host.UseSerilog();
        builder.Services.AddProblemDetails(c =>
        {
            //c.CustomizeProblemDetails = context =>
            //{
            //    context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
            //};
        });
        var config = new TypeAdapterConfig();
        config.Default.NameMatchingStrategy(NameMatchingStrategy.Flexible);
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddPollyPolicies();
        builder.Services.AddMapster(typeof(MappingProfile).Assembly);
        builder.HrmsConfigRabbitMq();
        builder.RegisterSelfServices();
        builder.Services.AddSignalR();
        builder.Services.RegisterHRCoreServices();
        builder.Services.RegisterDTRCoreServices();
        builder.Services.AddGeminiAiExtraction(builder.Configuration);
        builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(@"/app/dp-keys"));

        var app = builder.Build();
        // 1. FIRST: Parse headers from Nginx on localhost immediately
        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost
        };
        forwardedOptions.KnownNetworks.Clear();
        forwardedOptions.KnownProxies.Clear();
        app.UseForwardedHeaders(forwardedOptions);
        // 2. SECOND: API Documentation
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        var apiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.EnablePersistAuthorization();
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            foreach (var description in apiVersionProvider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"./{description.GroupName}/swagger.json",
                              $"HRMS API {description.ApiVersion}");
            }
            options.RoutePrefix = "swagger";
        });
        app.UseSerilogRequestLogging();
        app.UseRouting();
        app.UseCors("AllowAll");
        //app.UseMiddleware<ApiKeyMiddleware>();
        //app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHeaderPropagation();
        app.UseMiddleware<TenantDatabaseMiddleware>();
        app.MapControllers();
        app.MapHub<NotificationHub>(NotificationHub.Route);
        app.Run();
    }
}