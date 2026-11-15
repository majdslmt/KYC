using KYC.Application;
using Microsoft.AspNetCore.Diagnostics;

namespace KYC.Api;

/// <summary>
/// Centralizes exception-to-HTTP conversion so endpoints stay focused on use cases.
/// Detailed failures go to logs; public responses avoid exposing internals.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        // A client disconnect is not an application failure. Let ASP.NET Core
        // handle aborted requests without trying to send another response.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return false;

        // Map expected business cases to predictable statuses; an unexpected
        // exception gets a generic 500 response rather than a stack trace.
        var (status, message) = exception switch
        {
            InvalidSsnException => (400, exception.Message),
            CustomerNotFoundException => (404, exception.Message),
            UpstreamServiceException => (503, exception.Message),
            InvalidUpstreamDataException => (503, "Required customer data is unavailable."),
            _ => (500, "An unexpected error occurred while processing the request.")
        };

        if (status >= 500)
            logger.LogError(exception, "KYC aggregation failed with HTTP {StatusCode}.", status);
        else
            logger.LogInformation("KYC request rejected with HTTP {StatusCode}.", status);

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(message), cancellationToken);
        return true;
    }
}
