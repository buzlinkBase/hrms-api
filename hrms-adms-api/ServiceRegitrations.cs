
using Asp.Versioning;
using Hrms.adms.Controllers.Processors;
using Hrms.adms.Filters;
using MessagePack;
using MessagePack.AspNetCoreMvcFormatter;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Onepunch.Common.Lib.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hrms.adms;

public static class ServiceRegistrations
{
    public static void RegisterSelfServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddLogging();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<TenantConnectionInfo>();
        builder.Services.AddScoped<ITenantProvider, TenantProviderAccessor>(); 
        builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });

        builder.Services.AddScoped<IHMACService, HMACService>();
        builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMqSettings"));
        builder.Services.Configure<HMacSetting>(builder.Configuration.GetSection("HMacSettings"));
        builder.Services.Configure<ApiKeySetting>(builder.Configuration.GetSection("ApiKeySettings")); 
        //zkteco
        builder.Services.AddKeyedScoped<ICDataProcessor, AttLogTableProcessor>("ATTLOG");
        builder.Services.AddKeyedScoped<ICDataProcessor, OperLogProcessor>("OPERLOG");
        builder.Services.AddKeyedScoped<ICDataProcessor, UserInforTableProcessor>("USERINFO");
        builder.Services.AddKeyedScoped<ICDataProcessor, OptionsProcessor>("options");
        builder.Services.AddHeaderPropagation(options =>
        {
            options.Headers.Add("User-Agent");
            options.Headers.Add("Authorization");
            options.Headers.Add("X-Tenant-ID");
            options.Headers.Add("X-Api-Key");
        });

        var mpackOptions = MessagePackSerializerOptions.Standard
         .WithResolver(CompositeResolver.Create(
             OneMessagePackResolver.Instance,
             MessagePack.Resolvers.NativeDateTimeResolver.Instance,
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
        builder.Services.AddDbContext<AdmsContext>((options) =>
        {
            options.UseLazyLoadingProxies(true);
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });
        builder.Services
            .AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        // Swagger (defer versioned docs to Program.cs)
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
        builder.Services.AddSwaggerGen();
        var signingKey = builder.Configuration["JwtSettings:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "JWT SigningKey is not configured. Please set 'JwtSettings:SigningKey' in appsettings.json or environment variables."
            );
        }

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
         .AddJwtBearer(options =>
         {
             options.Events = new JwtBearerEvents
             {
                 OnAuthenticationFailed = context =>
                 {
                     Console.WriteLine("Auth failed: " + context.Exception.Message);
                     return Task.CompletedTask;
                 }
             };
             options.TokenValidationParameters = new TokenValidationParameters
             {
                 ValidateIssuer = true,
                 ValidIssuer = "Onepunch",
                 ValidateAudience = true,
                 ValidAudience = "Onepunch.AuthService",
                 ValidateLifetime = true,
                 ValidateIssuerSigningKey = true,
                 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
             };
         });
    }
}