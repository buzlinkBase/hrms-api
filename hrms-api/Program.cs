using Asp.Versioning.ApiExplorer;
using Hrms.Api.Extensions;
using Hrms.Api.Middlewares;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using MessagePack.AspNetCoreMvcFormatter;
using MessagePack.Resolvers;
using Hrms.Api.Filters;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Log.Logger = new LoggerConfiguration()
       .ReadFrom.Configuration(builder.Configuration)
       .CreateLogger();
        builder.Host.UseSerilog();

        var mpackOptions = MessagePackSerializerOptions.Standard
            .WithResolver(CompositeResolver.Create(
                // Priority 1: Compiled code (Fastest)
                OneMessagePackResolver.Instance,
                MessagePack.Resolvers.NativeDateTimeResolver.Instance,
                // Priority 2: Handling for dynamic/contractless if you still have old models
                MessagePack.Resolvers.ContractlessStandardResolver.Instance
            ))
            .WithCompression(MessagePackCompression.Lz4BlockArray);
        MessagePackSerializer.DefaultOptions = mpackOptions;

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<ResponseWrapperFilter>();
            var mpackOptions = ContractlessStandardResolver.Options
                .WithCompression(MessagePackCompression.Lz4BlockArray);

            options.InputFormatters.Add(new MessagePackInputFormatter(mpackOptions));
            options.OutputFormatters.Add(new MessagePackOutputFormatter(mpackOptions));
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

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

        builder.RegisterSelfServices();
        builder.Services.RegisterDTRCoreServices();
        builder.Services.RegisterHRCoreServices();
        builder.Services.AddScoped<ITenantProvider, TenantProvider>();
        builder.Services.AddAutoMapper(typeof(MappingProfile));
        builder.Services.AddAutoMapper(typeof(AspAutoMapperProfile));
        //builder.RegisterMessageHandlers();

        var app = builder.Build();
        var apiVersionProvider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
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
        app.Run();

    }
}