using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Hrms.Api;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var validApiKey = config["ApiKeySettings:ApiKey"];
        if (!context.HttpContext.Request.Headers.TryGetValue("Api-key", out var extractedApiKey) ||
            extractedApiKey != validApiKey)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}