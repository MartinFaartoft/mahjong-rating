using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Rating.Api.Infrastructure;

/// <summary>
/// Maps common application exceptions to appropriate HTTP status codes with a
/// small ProblemDetails-shaped payload. Registered via
/// <c>app.UseExceptionHandler(...)</c>.
/// </summary>
internal static class ProblemExceptionHandler
{
    public static async Task HandleAsync(HttpContext context)
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, title) = ex switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Server Error"),
        };
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title,
            status,
            detail = ex?.Message,
        });
    }
}
