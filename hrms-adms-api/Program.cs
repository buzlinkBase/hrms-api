using Asp.Versioning.ApiExplorer;
using Hrms.adms;
using Hrms.adms.Exceptions;
using Hrms.adms.Extensions;
using Hrms.adms.Middlewares;
using Mapster;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog.Events;

public class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        Log.Logger = new LoggerConfiguration()
       .ReadFrom.Configuration(builder.Configuration)
       // MassTransit's routine bus/transport lifecycle chatter (endpoint configuration, bus
       // start, message consumed) is Information-level and drowns out everything else in Seq --
       // baked in here rather than left to a per-environment Serilog:MinimumLevel:Override:
       // MassTransit env var, so it's never accidentally missing in a new environment.
       .MinimumLevel.Override("MassTransit", LogEventLevel.Error)
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
        builder.Services.RegisterAdmsCoreServices();
        builder.AdmsConfigRabbitMq();
        builder.RegisterSelfServices();
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
                              $"ADMS API {description.ApiVersion}");
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
        app.Run();
    }
}