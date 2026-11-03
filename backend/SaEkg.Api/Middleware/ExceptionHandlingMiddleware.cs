using System.Text.Json;
using SaEkg.Api.Contracts;
using Serilog.Context;

namespace SaEkg.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var requestId = RequestIdMiddleware.GetRequestId(context);
            using (LogContext.PushProperty("Module", "Api"))
            using (LogContext.PushProperty("Code", ErrorCodes.InternalError))
            using (LogContext.PushProperty("RequestId", requestId))
            {
                logger.LogError(ex, "Unhandled exception");
            }

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers[RequestIdMiddleware.HeaderName] = requestId;

            var error = new ApiError
            {
                Code = ErrorCodes.InternalError,
                Message = environment.IsDevelopment()
                    ? "Внутренняя ошибка сервера. Подробности см. в логах."
                    : "Внутренняя ошибка сервера.",
                Details = environment.IsDevelopment()
                    ? new { exceptionType = ex.GetType().Name }
                    : null,
                RequestId = requestId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
        }
    }
}
