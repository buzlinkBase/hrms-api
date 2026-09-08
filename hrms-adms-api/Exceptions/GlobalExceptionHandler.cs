using MessagePack;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Exceptions;

namespace Hrms.adms.Exceptions;

public sealed class GlobalExceptionHandler(IHostEnvironment env) // Use Primary Constructor for brevity
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            GuardException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = GetTitleForStatus(statusCode),
            Status = statusCode,
            Detail = exception.Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        // ONLY show sensitive details if we are debugging
        if (env.IsDevelopment())
        {
            problemDetails.Extensions.Add("stackTrace", exception.StackTrace);
            if (exception.InnerException != null)
            {
                problemDetails.Extensions.Add("innerException", exception.InnerException.Message);
            }
        }

        var response = new ResponseModel<ProblemDetails>
        {
            Status = statusCode,
            Message = "Error",
            Data = problemDetails
        };

        var acceptHeader = httpContext.Request.Headers.Accept.ToString();

        if (acceptHeader.Contains("application/x-msgpack"))
        {
            httpContext.Response.ContentType = "application/x-msgpack";
            await MessagePackSerializer.SerializeAsync(httpContext.Response.Body, response, MessagePackSerializer.DefaultOptions, cancellationToken);
        }
        else
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        }

        return true;
    }

    private static string GetTitleForStatus(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        _ => "An internal server error occurred"
    };
}