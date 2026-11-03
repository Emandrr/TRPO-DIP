using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaEkg.Api.Contracts;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Api.Middleware;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SaEkgDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && !context.Request.Path.StartsWithSegments("/api/v1/auth/change-password")
            && !context.Request.Path.StartsWithSegments("/api/v1/auth/logout")
            && !context.Request.Path.StartsWithSegments("/health"))
        {
            var mustChange = await db.Users.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => (bool?)x.MustChangePassword)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (mustChange == true)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                var error = new ApiError
                {
                    Code = ErrorCodes.PasswordChangeRequired,
                    Message = "Требуется смена пароля.",
                    RequestId = RequestIdMiddleware.GetRequestId(context)
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(error, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                return;
            }
        }

        await next(context);
    }
}
