
using Asp.Versioning;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Hrms.Api.Filters;
using Hrms.Api.Providers;
using Hrms.Core.Interfaces;
using Hrms.Infrastructure;
using MessagePack;
using MessagePack.AspNetCoreMvcFormatter;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Onepunch.Common.Lib.Cache;
using Onepunch.Common.Lib.DbServices;
using Onepunch.Common.Lib.Interfaces;
using Onepunch.Common.Lib.Security;
using Polly;
using Refit;
using StackExchange.Redis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hrms.Core.Messaging.LeaveWorkers;

namespace Hrms.Api.Extensions;

public static class ServiceRegistrationsExt
{
    public static void RegisterSelfServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddLogging();
        builder.Services.AddHttpContextAccessor();
        builder.RegisterSky();//db services
        builder.Services.AddScoped<TenantConnectionStringInfo>();
        builder.Services.AddScoped<ITenantProvider, TenantProviderAccessor>();
        builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });
        builder.Services.AddScoped<IHMACService, HMACService>();
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
        builder.Services.AddScoped<ICacheService, RedisCacheService>();

        builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMqSettings"));
        builder.Services.Configure<HMacSetting>(builder.Configuration.GetSection("HMacSettings"));
        builder.Services.Configure<ApiKeySetting>(builder.Configuration.GetSection("ApiKeySettings"));
        builder.Services.Configure<LeaveSchedulerOptions>(builder.Configuration.GetSection("LeaveScheduler"));
        builder.Services.AddHostedService<LeaveSchedulerService>(); // fires daily, discovers tenants from Leaves.TenantId
        builder.Services.AddScoped<LeaveDtrReconciliationService>();

        builder.Services.AddKeyedScoped<IFileParser, DatParser>(".dat");
        //builder.Services.AddKeyedScoped<IFileParser, CsvParser>(".csv");
        //builder.Services.AddKeyedScoped<IFileParser, TxtParser>(".txt");
        //builder.Services.AddKeyedScoped<IFileParser, ExcelParser>(".xls");

        var elasticSettings = new ElasticSettings();
        builder.Configuration.GetSection("ElasticSettings").Bind(elasticSettings);

        builder.Services.AddHeaderPropagation(options =>
        {
            options.Headers.Add("User-Agent");
            options.Headers.Add("Authorization");
            options.Headers.Add("X-Tenant-ID");
            options.Headers.Add("X-Api-Key");
        });

        var mpackOptions = MessagePackSerializerOptions.Standard
                .WithResolver(CompositeResolver.Create(
                    OneMessagePackResolver.Instance, // Your generated resolver
                    MessagePack.Resolvers.NativeDateTimeResolver.Instance,
                    MessagePack.Resolvers.ContractlessStandardResolver.Instance
                ))
                .WithCompression(MessagePackCompression.Lz4BlockArray);

        MessagePackSerializer.DefaultOptions = mpackOptions;

        //builder.Services.AddControllers(options =>
        //{
        //    options.Filters.Add<ResponseWrapperFilter>();
        //    options.InputFormatters.Add(new MessagePackInputFormatter(mpackOptions));
        //    options.OutputFormatters.Add(new MessagePackOutputFormatter(mpackOptions));
        //}).AddJsonOptions(options =>
        //{
        //    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        //    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        //    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        //}).AddNewtonsoftJson(options =>
        //{
        //    // 1. Create the NetTopologySuite serializer
        //    var serializer = NetTopologySuite.IO.GeoJsonSerializerFactory.Create();

        //    // 2. Loop through and add its specific GeoJSON converters to Newtonsoft
        //    foreach (var converter in serializer.Converters)
        //    {
        //        options.SerializerSettings.Converters.Add(converter);
        //    }
        //});

        builder.Services.AddControllers(options =>
        {
            options.Filters.Add<ResponseWrapperFilter>();
            options.Filters.Add<EmployeeOnlyRestrictionFilter>();
            options.RespectBrowserAcceptHeader = true;
        })
         .AddNewtonsoftJson(options =>
         {
             // 1. Core Newtonsoft Settings
             options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();
             options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
             options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
             // 2. Add NetTopologySuite GeoJSON converters
             // Use the static Create() method on GeoJsonSerializer itself
             var serializer = NetTopologySuite.IO.GeoJsonSerializer.Create();
             foreach (var converter in serializer.Converters)
             {
                 options.SerializerSettings.Converters.Add(converter);
             }
         }).AddMvcOptions(options =>
         {
             options.InputFormatters.Add(new MessagePackInputFormatter(mpackOptions));
             options.OutputFormatters.Add(new MessagePackOutputFormatter(mpackOptions));
         });

        builder.Services.AddRefitClient<IBranchClient>(new RefitSettings
        {
            ContentSerializer = new MessagePackContentSerializer(mpackOptions)
        })
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.Configuration["ApiServices:TenantService"]!))
        .AddHeaderPropagation();

        builder.Services.AddRefitClient<IConnectionClient>(new RefitSettings
        {
            ContentSerializer = new MessagePackContentSerializer(mpackOptions)
        })
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.Configuration["ApiServices:TenantService"]!))
        .AddHeaderPropagation();

        if (elasticSettings.Enable)
        {
            builder.Services.AddSingleton(sp =>
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
        builder.Services.AddDbContext<HrmsContext>((provider, optionsBuilder) =>
        {
            if (optionsBuilder.IsConfigured) return;
            var _tenantConnectionInfo = provider.GetRequiredService<TenantConnectionStringInfo>();
            var _tenantProvider = provider.GetRequiredService<ITenantProvider>();
            var connectionString = builder.Configuration.GetConnectionString("HrmsConnection");
            if (_tenantConnectionInfo != null && !string.IsNullOrWhiteSpace(_tenantConnectionInfo.ConnectionString))
            {
                connectionString = _tenantConnectionInfo.ConnectionString;
            }
            var serverVersion = ServerVersion.AutoDetect(connectionString);//new MySqlServerVersion(new Version(9, 2, 0));
            optionsBuilder.UseMySql(connectionString, serverVersion, x => x.UseNetTopologySuite());
            optionsBuilder.UseLazyLoadingProxies(true);
            optionsBuilder.AddInterceptors(new SoftDeleteInterceptor(), new ApplyTenantInterceptor(_tenantProvider));
            optionsBuilder.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
        });
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.WithOrigins(
                    "http://localhost:5173",
                    "http://localhost:4200",
                    "http://159.89.194.81:8001",
                    "http://159.89.194.81:8002",
                    "http://159.89.194.81:8003",
                    "http://165.232.166.164:8082",
                    "http://165.232.166.164:8083",
                    "http://165.232.166.164:8084",
                    "http://165.232.166.164:8085",
                    "http://165.232.166.164:8086",
                    "https://hris.onepunch.site",
                    "https://hris-dev.onepunch.site",
                    "https://hris-staging.onepunch.site",
                    "https://api.onepunch.site")
                      .AllowCredentials()
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
        // Flow J: HRIS validates JWTs independently via Auth's JWKS endpoint rather than a
        // shared secret — it never queries Auth's Redis cache or asks Account Service to
        // authorize on its behalf. AllowLegacyHmacValidation keeps the old shared-secret path
        // alive as a fallback during the platform's HMAC->RSA/JWKS migration window.
        builder.Services.AddHttpClient<JwksClient>(client =>
        {
            var authUrl = builder.Configuration["ApiServices:AuthService"] ?? "";
            client.BaseAddress = new Uri(authUrl.TrimEnd('/') + "/");
        });

        builder.Services.AddAuthorizationBuilder()
         .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
         .AddJwtBearer(options =>
         {
             // AuthApi's JwtService.CreateTokenAsync mints "sub"/"email" as the short registered
             // JWT claim names -- without this, ASP.NET Core's default inbound map silently
             // rewrites them to long ClaimTypes.* URIs when building the ClaimsPrincipal, which is
             // why "sub" reads worked (GetRequiredUserId expected the rewritten form) while
             // "email" reads silently returned null (GetUserClaim("email") expected the raw
             // form). See Extensions\HttpRequestExtensions.cs, which now reads "sub" directly.
             options.MapInboundClaims = false;
             options.Events = new JwtBearerEvents
             {
                 // SignalR's JS client sends the token as ?access_token= on the query string for
                 // the negotiate/WebSocket handshake, not as an Authorization header — without
                 // this, NotificationHub connections 401 before ever reaching the hub (it has no
                 // [Authorize] of its own, but the RequireAuthenticatedUser() fallback policy
                 // registered above still applies to it).
                 OnMessageReceived = context =>
                 {
                     var accessToken = context.Request.Query["access_token"];
                     if (!string.IsNullOrEmpty(accessToken) &&
                         context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                     {
                         context.Token = accessToken;
                     }
                     return Task.CompletedTask;
                 },

                 OnAuthenticationFailed = context =>
                 {
                     Console.WriteLine("Auth failed: " + context.Exception.Message);
                     return Task.CompletedTask;
                 },

                 OnChallenge = async context =>
                 {
                     // Skip the default response
                     context.HandleResponse();

                     context.Response.StatusCode = 401;
                     context.Response.ContentType = "application/json";

                     var errorDetail = new ProblemDetails
                     {
                         Type = $"https://httpstatuses.com/{401}",
                         Title = "Unauthorized",
                         Status = (int)HttpStatusCode.Unauthorized,
                         Detail = "Unauthorized. Token is missing or invalid.",
                         Instance = $"{context.Request.Method} {context.Request.Path}"
                     };
                     var response = new ResponseModel<ProblemDetails>
                     {
                         Message = errorDetail.Detail,
                         Status = (int)HttpStatusCode.Unauthorized,
                         Data = errorDetail
                     };
                     await context.Response.WriteAsJsonAsync(response);
                 },

                 // ✅ Add this — fires when token is valid but user lacks permission
                 OnForbidden = async context =>
                 {
                     context.Response.StatusCode = 403;
                     context.Response.ContentType = "application/json";

                     var errorDetail = new ProblemDetails
                     {
                         Type = $"https://httpstatuses.com/{403}",
                         Title = "Forbidden",
                         Status = (int)HttpStatusCode.Forbidden,
                         Detail = "Forbidden. You do not have permission to access this resource.",
                         Instance = $"{context.Request.Method} {context.Request.Path}"
                     };
                     var response = new ResponseModel<ProblemDetails>
                     {
                         Message = errorDetail.Detail,
                         Status = (int)HttpStatusCode.Unauthorized,
                         Data = errorDetail
                     };

                     await context.Response.WriteAsJsonAsync(response);
                 }
             };
             options.TokenValidationParameters = new TokenValidationParameters
             {
                 ValidateIssuer = true,
                 ValidIssuer = "Onepunch",
                 ValidateAudience = true,
                 ValidAudience = "Onepunch.AuthService",
                 ValidateLifetime = true,
                 ValidateIssuerSigningKey = true
             };
         });

        // Resolve JwksClient lazily from the real (post-Build) app container instead of a
        // throwaway one built eagerly here.
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IHttpClientFactory>((options, httpClientFactory) =>
            {
                var authUrl = (builder.Configuration["ApiServices:AuthService"] ?? "").TrimEnd('/') + "/";
                var legacySigningKey = builder.Configuration["JwtSettings:SigningKey"];
                var allowLegacyHmac = builder.Configuration.GetValue<bool?>("JwtSettings:AllowLegacyHmacValidation") ?? true;

                List<SecurityKey> cachedKeys = new();
                DateTime cacheExpiry = DateTime.MinValue;
                object cacheLock = new();

                options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                {
                    bool stale;
                    lock (cacheLock) stale = DateTime.UtcNow > cacheExpiry;

                    if (stale)
                    {
                        try
                        {
                            var client = httpClientFactory.CreateClient();
                            var json = client.GetStringAsync($"{authUrl}.well-known/jwks.json").GetAwaiter().GetResult();
                            var jwks = new JsonWebKeySet(json);
                            lock (cacheLock)
                            {
                                cachedKeys = jwks.GetSigningKeys().ToList();
                                cacheExpiry = DateTime.UtcNow.AddMinutes(10);
                            }
                        }
                        catch { }
                    }

                    List<SecurityKey> keys;
                    lock (cacheLock) keys = cachedKeys.ToList();

                    if (allowLegacyHmac && !string.IsNullOrEmpty(legacySigningKey))
                        keys.Add(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(legacySigningKey)));

                    return keys;
                };
            });
    }
}


public static class DODbServiceRegistrationExtensions
{
    public static void RegisterDO(this WebApplicationBuilder builder)
    {
        var doToken = builder.Configuration["DigitalOcean:ApiToken"];
        builder.Services.AddHttpClient<IDbService, DigitalOceanDbService>(client =>
        {
            // Root address
            client.BaseAddress = new Uri("https://api.digitalocean.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddResilienceHandler("do-heavy-ops", pipeline =>
        {
            // 1. Give the overall operation 2 minutes
            pipeline.AddTimeout(TimeSpan.FromMinutes(2));
            //// 2. Add a Retry strategy for transient network blips
            pipeline.AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                UseJitter = true,
                BackoffType = Polly.DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2)
            });
            //// 3. Keep a Circuit Breaker, but make it less sensitive
            pipeline.AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromMinutes(5),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30)
            });
        });
    }
}

public static class SkyDbServiceRegistrationExtensions
{
    public static void RegisterSky(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("HrmsConnection")!;
        builder.Services.AddScoped<IDbService>(sp => new SkySqlDbService(connectionString));
    }
}