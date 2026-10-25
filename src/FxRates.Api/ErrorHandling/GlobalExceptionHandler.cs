using FxRates.Application.Exceptions;
using FxRates.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace FxRates.Api.ErrorHandling;

/// <summary>Maps exceptions to RFC 7807 problem details and logs them.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Maps the exception to a status code, logs it and writes the problem details response.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
            RateAlreadyExistsException => (StatusCodes.Status409Conflict, "Rate already exists"),
            ExternalProviderUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Rate provider unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status >= 500) 
            logger.LogError(exception, "Request failed: {Title}", title);
        else 
            logger.LogWarning("Request rejected: {Message}", exception.Message);

        context.Response.StatusCode = status;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                // never leak internal details on 500
                Detail = status >= 500 ? null : exception.Message
            }
        });
    }
}