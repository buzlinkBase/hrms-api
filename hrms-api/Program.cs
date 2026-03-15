using Asp.Versioning.ApiExplorer;
using Hrms.Api.Extensions;
using Hrms.Api.Middlewares;
using Hrms.Core.Extensions;
using Serilog;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        //LOGGER
        Log.Logger = new LoggerConfiguration()
       .ReadFrom.Configuration(builder.Configuration)
       .CreateLogger();
        builder.Host.UseSerilog(); 
        //builder.Services.Configure<ApiBehaviorOptions>(options =>
        //{
        //    // Stops the default framework behavior of returning a 400 immediately
        //    options.SuppressModelStateInvalidFilter = true;
        //});
        //builder.Services.AddProblemDetails(c =>
        //{
        //    //c.CustomizeProblemDetails = context =>
        //    //{
        //    //    context.ProblemDetails.Extensions.TryAdd("requestId", context.HttpContext.TraceIdentifier);
        //    //};
        //});
        //builder.Services.AddExceptionHandler<GlobalExceptionHandler>();  
        builder.Services.AddPollyPolicies();
        builder.HrmsConfigRabbitMq();
        builder.RegisterSelfServices();
        builder.Services.RegisterHRCoreServices();
        builder.Services.RegisterDTRCoreServices();
        builder.Services.AddAutoMapper(typeof(MappingProfile));
        builder.Services.AddAutoMapper(typeof(AspAutoMapperProfile));
        //builder.WebHost.UseUrls("http://0.0.0.0:7237");

        var app = builder.Build();
        var apiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.EnablePersistAuthorization();
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            foreach (var description in apiVersionProvider.ApiVersionDescriptions)
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json",
                                        $"HRMS API {description.ApiVersion}");
            }
        });

        //app.UseExceptionHandler();
        app.UseRouting();
        app.UseCors("AllowAll");
        //app.UseMiddleware<ApiKeyMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        //app.Use(async (context, next) =>
        //{
        //    // This logs EVERY single request that hits your server
        //    var path = context.Request.Path;
        //    Console.WriteLine($"[GLOBAL SNIFFER] Request received for: {path}");

        //    // Continue to the next middleware
        //    await next();
        //});
        app.Run();

    }
}