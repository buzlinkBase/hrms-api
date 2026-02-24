using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Hrms.Api.Exceptions
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetailsService;

        public GlobalExceptionHandler(IProblemDetailsService problemDetailsService)
        {
            _problemDetailsService = problemDetailsService;
        }
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var statusCode = exception switch
            {
                GuardException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError,
            };

            httpContext.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{statusCode}",
                Title = "An error occurred",
                Status = statusCode,
                Detail = exception.Message, // This gives you the "Object reference..."
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}"
            };

            // ADD THIS: Include StackTrace only if in Development environment
            // This makes the error "meaningful" for you.
            problemDetails.Extensions.Add("stackTrace", exception.StackTrace);

            if (exception.InnerException != null)
            {
                problemDetails.Extensions.Add("innerException", exception.InnerException.Message);
            }

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemDetails
            });
        }
    }

}
