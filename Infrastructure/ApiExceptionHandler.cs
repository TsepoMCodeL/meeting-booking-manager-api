using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

// Converts known request and SQLite lock failures into safe HTTP problem responses.
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = FindSqliteBusyException(exception) is not null
            ? StatusCodes.Status503ServiceUnavailable
            : exception is BadHttpRequestException { StatusCode: >= 400 and < 500 } badRequest
                ? badRequest.StatusCode
                : 0;
        var title = statusCode switch
        {
            StatusCodes.Status503ServiceUnavailable => "Database temporarily unavailable",
            0 => string.Empty,
            _ => "Invalid request"
        };
        var detail = statusCode switch
        {
            StatusCodes.Status503ServiceUnavailable => "The database is busy. Please retry the request.",
            0 => string.Empty,
            _ => "The request could not be read. Check the URL and request body."
        };

        if (statusCode == 0)
            return false;

        if (statusCode == StatusCodes.Status503ServiceUnavailable)
            logger.LogWarning(FindSqliteBusyException(exception), "SQLite is busy handling {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogInformation("Rejected unreadable request {Method} {Path} with status {StatusCode}", httpContext.Request.Method, httpContext.Request.Path, statusCode);
        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }

    private static SqliteException? FindSqliteBusyException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current is SqliteException { SqliteErrorCode: 5 or 6 } sqliteException)
                return sqliteException;
        }

        return null;
    }
}
