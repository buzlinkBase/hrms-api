using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hrms.adms.Filters;

/// <summary>
/// Service-to-service gate: header "Api-key" must equal config "ApiKeySettings:ApiKey". Mirrors
/// hrms-api's Hrms.Api.ApiKeyAttribute. With no key configured, every call is refused.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var validApiKey = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["ApiKeySettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(validApiKey)
            || !context.HttpContext.Request.Headers.TryGetValue("Api-key", out var extractedApiKey)
            || extractedApiKey != validApiKey)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        await next();
    }
}
