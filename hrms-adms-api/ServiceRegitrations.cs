
using Asp.Versioning;
using Hrms.adms.Filters;
using Hrms.adms.Models.DTO;
using Hrms.adms.Services;
using Hrms.adms.Services.Processors;
using MessagePack;
using MessagePack.AspNetCoreMvcFormatter;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Onepunch.Common.Lib.DbServices;
using Onepunch.Common.Lib.Security;
using Polly;
using Refit;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
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
        builder.Services.AddScoped<IUnitOfWorkService, UnitOfWorkService>();
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
            options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
        MessagePackSerializer.DefaultOptions = mpackOptions;
        builder.Services.AddRefitClient<IConnectionClient>(new RefitSettings
        {
            ContentSerializer = new MessagePackContentSerializer(mpackOptions)
        })
        .ConfigureHttpClient(c => c.BaseAddress = new Uri(builder.Configuration["ApiServices:TenantService"]!))
        .AddHeaderPropagation();


        builder.Services.AddScoped<ITenantProvider, TenantProviderAccessor>();
        builder.Services.AddScoped<IMigrationService, EvolveMigrationService>();
        builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });

        builder.Services.AddScoped<IHMACService, HMACService>();
        builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMqSettings"));
        //zkteco
        builder.Services.AddScoped<ISystemClockService, SystemClockService>();
        builder.Services.AddKeyedScoped<ICDataProcessor, AttLogTableProcessor>("ATTLOG");
        //builder.Services.AddKeyedScoped<ICDataProcessor, OperLogProcessor>("BIODATA"); bio template
        builder.Services.AddKeyedScoped<ICDataProcessor, OperLogProcessor>("OPERLOG");
        builder.Services.AddKeyedScoped<ICDataProcessor, UserInforTableProcessor>("USERINFO");
        builder.Services.AddKeyedScoped<ICDataProcessor, OptionsProcessor>("options");
        builder.Services.AddHostedService<CleanUpDormantCommandWatcher>();

        builder.Services.AddHeaderPropagation(options =>
        {
            options.Headers.Add("User-Agent");
            options.Headers.Add("Authorization");
            options.Headers.Add("X-Tenant-ID");
            options.Headers.Add("X-Api-Key");
        });

        builder.Services.AddDbContext<AdmsContext>((sp, options) =>
        {
            var connectionString = builder.Configuration.GetConnectionString("AdmsConnection");
            options.UseLazyLoadingProxies(true);
            var serverVersion = new MySqlServerVersion(new Version(9, 2, 0));
            options.UseMySql(connectionString, serverVersion);
            var tp = sp.GetRequiredService<ITenantProvider>();
            options.AddInterceptors(new SoftDeleteInterceptor(), new ApplyTenantInterceptor(tp));
            options.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();

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
                    "http://198.211.112.14:8082",
                    "http://198.211.112.14:8083",
                    "http://198.211.112.14:8084",
                    "http://198.211.112.14:8085",
                    "http://198.211.112.14:8086",
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
        builder.Services.AddSwaggerGen(options =>
        {
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
        });
        // Flow J: validate independently via Auth's JWKS endpoint rather than a shared secret.
        // AllowLegacyHmacValidation keeps the old shared-secret path alive as a fallback during
        // the platform's HMAC->RSA/JWKS migration window.
        builder.Services.AddHttpClient<JwksClient>(client =>
        {
            var authUrl = builder.Configuration["ApiServices:AuthService"] ?? "";
            client.BaseAddress = new Uri(authUrl);
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
             options.Events = new JwtBearerEvents
             {
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