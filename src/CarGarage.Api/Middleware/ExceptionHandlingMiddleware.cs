using CarGarage.Application.Exceptions;

namespace CarGarage.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException error)
        {
            context.Response.StatusCode = error.Kind switch
            {
                AppError.Validation => 400,
                AppError.Unauthorized => 401,
                AppError.Conflict => 409,
                _ => 500
            };
            await context.Response.WriteAsJsonAsync(new { error = error.Message, details = error.Details });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; no response can be delivered.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled error during {Method} {Path}",
                context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
        }
    }
}
