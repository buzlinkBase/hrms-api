//using Asp.Versioning; 
//using Microsoft.AspNetCore.Diagnostics;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.OpenApi.Models;

//namespace Hrms.Api.Messaging;

//public static class RegisterServicesExt
//{
//    public static void RegisterServices(this WebApplicationBuilder builder)
//    {


//        builder.Services.AddDbContext<UserContext>((provider, x) =>
//        {
//            var connection = builder.Configuration.GetConnectionString("DefaulConnection");
//            x.UseMySql(connection, ServerVersion.AutoDetect(connection));
//        });
//        builder.Services.AddSwaggerGen(c =>
//        {
//            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Easyfs Tenant Service", Version = "v1" });
//            c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
//            {
//                Description = "API Key needed to access the endpoints. Use 'Api-Key: {your-key}'",
//                In = ParameterLocation.Header,
//                Name = "Api-Key",
//                Type = SecuritySchemeType.ApiKey,
//                Scheme = "ApiKeyScheme"
//            });
//            c.AddSecurityRequirement(new OpenApiSecurityRequirement
//                {
//                        {
//                            new OpenApiSecurityScheme
//                            {
//                                Reference = new OpenApiReference
//                                {
//                                    Type = ReferenceType.SecurityScheme,
//                                    Id = "ApiKey"
//                                }
//                            },
//                            Array.Empty<string>()
//                        }
//                });
//        });
//        builder.Services.AddApiVersioning(options =>
//        {
//            options.DefaultApiVersion = new ApiVersion(1, 0);
//            options.AssumeDefaultVersionWhenUnspecified = true;
//            options.ReportApiVersions = true;
//            options.ApiVersionReader = ApiVersionReader.Combine(
//                new UrlSegmentApiVersionReader(),
//                new HeaderApiVersionReader("X-Api-Version")
//            );
//        })
//               .AddApiExplorer(options =>
//               {
//                   options.GroupNameFormat = "'v'VVV";
//                   options.SubstituteApiVersionInUrl = true;
//               });


//        builder.Services.Configure<ApiBehaviorOptions>(options =>
//        {
//            options.InvalidModelStateResponseFactory = context =>
//            {
//                var errors = context.ModelState
//                .Where(x => x.Value.Errors.Count > 0)
//                .ToDictionary(
//                    kvp => kvp.Key,
//                    kvp => kvp.Value.Errors.Select(e => e.ErrorMessage).ToArray()
//                );

//                var problem = new ValidationProblemDetails(errors)
//                {
//                    Title = "Invalid Payload",
//                    Status = StatusCodes.Status400BadRequest,
//                    Detail = "One or more validation errors occurred.",
//                    Instance = context.HttpContext.Request.Path
//                };

//                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
//                problem.Extensions["timestamp"] = DateTime.UtcNow;

//                return new BadRequestObjectResult(problem)
//                {
//                    ContentTypes = { "application/problem+json" }
//                };
//            };
//        });

//    }
//}