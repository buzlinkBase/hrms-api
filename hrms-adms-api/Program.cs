using Asp.Versioning.ApiExplorer;
using Hrms.adms;
using Hrms.adms.Extensions;
using Hrms.adms.Middlewares;
using Microsoft.AspNetCore.DataProtection;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        Log.Logger = new LoggerConfiguration()
       .ReadFrom.Configuration(builder.Configuration)
       .CreateLogger();
        builder.Host.UseSerilog(); 
        builder.Services.AddPollyPolicies();
        builder.AdmsConfigRabbitMq();
        builder.RegisterSelfServices();
        builder.Services.RegisterHRCoreServices();
        builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(@"/app/dp-keys"));

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
        app.UseSerilogRequestLogging();
        app.UseHeaderPropagation();
        app.UseMiddleware<TenantDatabaseMiddleware>();
        app.MapControllers();
        app.Run();
    }
}