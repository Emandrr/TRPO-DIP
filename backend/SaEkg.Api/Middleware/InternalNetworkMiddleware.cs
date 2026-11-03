using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SaEkg.Api.Contracts;
using SaEkg.Infrastructure.Auth;

namespace SaEkg.Api.Middleware;

public sealed class InternalNetworkMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var security = options.Value;
        if (!security.EnforceInternalNetworkCheck
            || context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);
        if (role is not null
            && security.ExternalAccessAllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && IsInternal(remote, security.InternalNetworks))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        var error = new ApiError
        {
            Code = ErrorCodes.InternalNetworkOnly,
            Message = "Доступ для этой роли разрешён только из внутренней сети.",
            RequestId = RequestIdMiddleware.GetRequestId(context)
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private static bool IsInternal(IPAddress address, string[] networks)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        foreach (var cidr in networks)
        {
            if (TryParseCidr(cidr, out var network, out var prefix) && IsInSubnet(address, network, prefix))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseCidr(string cidr, out IPAddress network, out int prefix)
    {
        network = IPAddress.None;
        prefix = 0;
        var parts = cidr.Split('/', 2);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out network!) || !int.TryParse(parts[1], out prefix))
        {
            return false;
        }

        return true;
    }

    private static bool IsInSubnet(IPAddress address, IPAddress network, int prefix)
    {
        if (address.AddressFamily != network.AddressFamily)
        {
            return false;
        }

        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (addressBytes.Length != networkBytes.Length)
        {
            return false;
        }

        var fullBytes = prefix / 8;
        var remainingBits = prefix % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)~(0xFF >> remainingBits);
        return (addressBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }
}
