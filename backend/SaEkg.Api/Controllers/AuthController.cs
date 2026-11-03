using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SaEkg.Api.Contracts;
using SaEkg.Application.Auth;
using SaEkg.Api.Middleware;

namespace SaEkg.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await auth.RegisterPatientAsync(request, cancellationToken));
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await auth.LoginAsync(request, cancellationToken));
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await auth.RefreshAsync(request, cancellationToken));
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        try
        {
            await auth.ChangePasswordAsync(userId, request, cancellationToken);
            return NoContent();
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    private ObjectResult AuthError(AuthException ex)
    {
        var status = ex.Code switch
        {
            ErrorCodes.Conflict => StatusCodes.Status409Conflict,
            ErrorCodes.ValidationError => StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.AccountLocked => StatusCodes.Status423Locked,
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status401Unauthorized
        };

        return StatusCode(status, new ApiError
        {
            Code = ex.Code,
            Message = ex.Message,
            Details = ex.Details,
            RequestId = RequestIdMiddleware.GetRequestId(HttpContext)
        });
    }
}
