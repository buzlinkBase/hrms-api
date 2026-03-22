using Asp.Versioning.ApiExplorer;
using Hrms.adms;
using Hrms.adms.Extensions;
using Hrms.adms.Middleware;

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
        builder.Services.AddPollyPolicies();
        builder.AdmsConfigRabbitMq();
        builder.RegisterSelfServices();
        builder.Services.RegisterHRCoreServices();
        //builder.WebHost.UseUrls("https://0.0.0.0:7052");
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

        app.UseRouting();
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHeaderPropagation();
        app.UseMiddleware<TenantDatabaseMiddleware>();
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