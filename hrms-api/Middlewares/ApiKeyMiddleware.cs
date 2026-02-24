namespace Hrms.Api.Middlewares;

//if apply to all route
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _validApiKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _validApiKey = config["ApiKeySettings:ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("Api-Key", out var extractedApiKey) ||
            extractedApiKey != _validApiKey)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Unauthorized: Invalid API Key");
            return;
        }

        await _next(context);
    }
}