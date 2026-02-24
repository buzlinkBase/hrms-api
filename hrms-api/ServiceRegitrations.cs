

using Asp.Versioning;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Hrms.Api.Messaging;
using Hrms.Api.Providers;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.Text;

namespace Hrms.Api.Extensions
{
    public static class ServiceRegistrations
    {
        public static void RegisterSelfServices(this WebApplicationBuilder builder)
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IDbConnectionProvider, EfConnectionMetadataProvider>();
            builder.Services.AddScoped<IAppConfigurationProvider, WebAppConfigurationProvider>();
            builder.Services.AddScoped<ITenantContextAccessor, WebTenantContextAccessor>();
            builder.Services.AddLogging();
            builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });
            builder.Services.AddScoped<IHMACService, HMACService>();
            builder.Services.Configure<HMacSetting>(builder.Configuration.GetSection("HMacSettings"));
            builder.Services.Configure<ApiKeySetting>(builder.Configuration.GetSection("ApiKeySettings"));
            builder.Services.AddScoped<ICacheService, RedisCacheService>();
            var elasticSettings = new ElasticSettings();
            builder.Configuration.GetSection("ElasticSettings").Bind(elasticSettings);
            builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configuration = builder.Configuration.GetConnectionString("Redis");
                return ConnectionMultiplexer.Connect(configuration);
            });

            if (elasticSettings.Enable)
            {
                builder.Services.AddSingleton<ElasticsearchClient>(sp =>
                {
                    var url = elasticSettings.Url; // Use https if SSL is enabled
                    var user = elasticSettings.User;
                    var pass = elasticSettings.Password;
                    var settings = new ElasticsearchClientSettings(new Uri(url))
                   .Authentication(new BasicAuthentication(user, pass))
                   .ServerCertificateValidationCallback((sender, cert, chain, errors) => true);
                    return new ElasticsearchClient(settings);
                });
                builder.Services.AddSingleton<ISearchEngineService, ElasticSearchService>();
            }
            else
            {
                builder.Services.AddSingleton<ISearchEngineService, NullSearchService>();
            }

            builder.Services.AddDbContext<HrmsContext>((provider, options) =>
            {
                var tenantAccessor = provider.GetRequiredService<ITenantContextAccessor>();
                var tenantProvider = provider.GetRequiredService<ITenantProvider>();
                var conProvider = provider.GetRequiredService<IDbConnectionProvider>();
                var appConfig = provider.GetRequiredService<IAppConfigurationProvider>();

                var tenantId = tenantAccessor.GetTenantId();
                var defaultConn = appConfig.GetConnectionString("DefaultConnection");
                var tenantConn = conProvider.GetConnectionString(tenantId);
                var connectionString = tenantConn ?? defaultConn!;

                if (tenantProvider.TenantId == Guid.Empty)
                    tenantProvider.SetTenantId(tenantId);

                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                options.AddInterceptors(new ApplyTenantInterceptor(tenantProvider));
                options.AddInterceptors(new SoftDeleteInterceptor());
                options.UseLazyLoadingProxies(true);
                options.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
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

            builder.Services.AddSwaggerGen(options =>
            {
                options.OperationFilter<SwaggerHeader>();
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "OnePunch HRMS API",
                    Version = "v1"
                });

                // JWT Bearer
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' [space] and then your valid JWT token.\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6...\""
                });

                // API Key
                options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                {
                    Description = "API Key needed to access the endpoints. Example: \"X-Api-Key: {key}\"",
                    Name = "X-Api-Key",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "ApiKeyScheme"
                });

                // Apply both globally
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        } });
            });

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
                         // This will print the EXACT reason for the 401 in your console
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
}