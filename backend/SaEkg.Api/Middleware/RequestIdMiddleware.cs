namespace SaEkg.Api.Middleware;

public sealed class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-Id";
    public const string ItemKey = "RequestId";

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestId))
        {
            requestId = Guid.NewGuid().ToString("N");
        }

        context.Items[ItemKey] = requestId;
        context.Response.Headers[HeaderName] = requestId;
        using (Serilog.Context.LogContext.PushProperty("RequestId", requestId))
        {
            await next(context);
        }
    }

    public static string GetRequestId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string id
            ? id
            : string.Empty;
}
