using Hrms.Api.Extensions;
using Hrms.Core.Services.Compliance;
using MessagePack;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Onepunch.Common.Lib.Exceptions;
using Onepunch.Common.Lib.Subscriptions;

namespace Hrms.Api.Exceptions;

public sealed class GlobalExceptionHandler(IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            AccountNotLinkedException => StatusCodes.Status400BadRequest,
            GuardException => StatusCodes.Status400BadRequest,
            ValidationException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            NotFoundException => StatusCodes.Status404NotFound,
            ForbiddenException => StatusCodes.Status403Forbidden,
            DeviationAcknowledgmentRequiredException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        httpContext.Response.StatusCode = statusCode;

        // 1. Create the detailed error object (ProblemDetails)
        var errorDetail = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = GetTitleForStatus(statusCode),
            Status = statusCode,
            // Services can't know who's calling -- name the caller's own login email here, since
            // that's exactly what an admin needs to put on the Employee record to link them.
            Detail = exception is AccountNotLinkedException notLinked
                ? notLinked.MessageFor(httpContext.User.GetUserClaim("email"))
                : exception.Message,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
        };

        // A subscription refusal: the frontend switches on Type to show an upgrade prompt
        // instead of a generic error, and needs no stack trace.
        if (exception is PlanRestrictionException restriction)
        {
            errorDetail.Type = restriction.ProblemType;
            errorDetail.Title = "Not included in your plan";
            foreach (var (key, value) in restriction.Extensions) errorDetail.Extensions[key] = value;
            await WriteAsync(httpContext, new ResponseModel<ProblemDetails> { Status = statusCode, Message = "Error", Data = errorDetail }, cancellationToken);
            return true;
        }

        // A setting that differs from the DOLE defaults: the frontend lists the deviations, asks the
        // admin to acknowledge them, and repeats the request with acknowledgeDeviations=true.
        if (exception is DeviationAcknowledgmentRequiredException deviation)
        {
            errorDetail.Type = DeviationAcknowledgmentRequiredException.ProblemType;
            errorDetail.Title = "Acknowledgment required";
            // Plain dictionaries rather than the record, so MessagePack serializes them without a resolver.
            errorDetail.Extensions["deviations"] = deviation.Deviations.Select(d => new Dictionary<string, string>
            {
                ["key"] = d.Key,
                ["label"] = d.Label,
                ["statutoryValue"] = d.StatutoryValue,
                ["configuredValue"] = d.ConfiguredValue,
                ["note"] = d.Note,
            }).ToList();
            await WriteAsync(httpContext, new ResponseModel<ProblemDetails> { Status = statusCode, Message = "Error", Data = errorDetail }, cancellationToken);
            return true;
        }

        //if (env.IsDevelopment())
        //{
        errorDetail.Extensions.Add("stackTrace", exception.StackTrace);
        if (exception.InnerException != null)
        {
            errorDetail.Extensions.Add("innerException", exception.InnerException.Message);
        }
        //}
        // 2. Wrap it in your standard ResponseModel
        var response = new ResponseModel<ProblemDetails>
        {
            Status = statusCode,
            Message = "Error",
            Data = errorDetail
        };

        await WriteAsync(httpContext, response, cancellationToken);
        return true; // Mark as handled
    }

    // 3. Determine Content-Type (Negotiate between MsgPack and JSON)
    private static async Task WriteAsync(HttpContext httpContext, ResponseModel<ProblemDetails> response, CancellationToken cancellationToken)
    {
        var acceptHeader = httpContext.Request.Headers.Accept.ToString();

        if (acceptHeader.Contains("application/x-msgpack"))
        {
            httpContext.Response.ContentType = "application/x-msgpack";
            // Use the DefaultOptions you defined in Program.cs (which includes LZ4)
            await MessagePackSerializer.SerializeAsync(httpContext.Response.Body, response, MessagePackSerializer.DefaultOptions, cancellationToken);
        }
        else
        {
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        }
    }

    private static string GetTitleForStatus(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        422 => "Validation Error",
        500 => "Server Error",
        _ => "An internal server error occurred"
    };
}