using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SaEkg.Api.Contracts;
using SaEkg.Infrastructure.Persistence;

namespace SaEkg.Api.Middleware;

public sealed class BlockedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SaEkgDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var blocked = await db.Users.AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => (bool?)x.IsBlocked)
                .FirstOrDefaultAsync(context.RequestAborted);

            if (blocked == true)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                var error = new ApiError
                {
                    Code = ErrorCodes.Forbidden,
                    Message = "Учётная запись заблокирована.",
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
