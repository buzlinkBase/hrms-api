namespace Hrms.Api.Middlewares;

public class CorrelationIdMiddleware
{
    private const string HeaderKey = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.ContainsKey(HeaderKey)
            ? context.Request.Headers[HeaderKey].ToString()
            : Guid.NewGuid().ToString();

        context.Items[HeaderKey] = correlationId;
        context.Response.Headers[HeaderKey] = correlationId;

        await _next(context);
    }
}