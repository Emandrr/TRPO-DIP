using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Api.Contracts;
using SaEkg.Api.Middleware;
using SaEkg.Application.Auth;
using SaEkg.Domain.Enums;

namespace SaEkg.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRole.Admin)]
public sealed class AdminUsersController(IAdminUserService users) : ControllerBase
{
    [HttpGet("api/v1/admin/users")]
    public async Task<ActionResult> List(
        [FromQuery] string? role,
        [FromQuery] bool? blocked,
        [FromQuery] string? email,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
        => Ok(await users.ListAsync(new UserQuery(role, blocked, email, page, pageSize), cancellationToken));

    [HttpPost("api/v1/admin/users")]
    public async Task<ActionResult<CreatedStaffUserResponse>> Create([FromBody] CreateStaffUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await users.CreateStaffAsync(request, cancellationToken);
            return Created($"/api/v1/admin/users/{created.User.Id}", created);
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    [HttpPut("api/v1/admin/users/{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await users.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    [HttpPost("api/v1/admin/users/{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id, CancellationToken cancellationToken)
        => await users.SetBlockedAsync(id, true, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("api/v1/admin/users/{id:guid}/unblock")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken cancellationToken)
        => await users.SetBlockedAsync(id, false, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("api/v1/admin/users/{id:guid}/temp-password")]
    public async Task<ActionResult<TempPasswordResponse>> TempPassword(Guid id, CancellationToken cancellationToken)
    {
        var result = await users.ResetTempPasswordAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("api/v1/admin/users/{id:guid}/temp-password.pdf")]
    public IActionResult TempPasswordPdf(Guid id, [FromServices] IPendingTempPasswordStore pending, [FromServices] ITempPasswordPdfService pdf)
    {
        if (!pending.TryTake(id, out var item) || item is null)
        {
            return StatusCode(StatusCodes.Status410Gone, new ApiError
            {
                Code = ErrorCodes.NotFound,
                Message = "PDF временного пароля недоступен (уже выдан или истёк).",
                RequestId = RequestIdMiddleware.GetRequestId(HttpContext)
            });
        }

        var bytes = pdf.Render(item.Email, item.TemporaryPassword);
        return File(bytes, "application/pdf", $"temp-password-{id:N}.pdf");
    }

    [HttpPost("api/v1/admin/patients/{id:guid}/consent")]
    public async Task<IActionResult> Consent(Guid id, CancellationToken cancellationToken)
        => await users.SetConsentAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPut("api/v1/admin/assignments")]
    public async Task<IActionResult> Assign([FromBody] AssignmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var adminId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
                ? parsed
                : Guid.Empty;
            await users.AssignAsync(request, adminId, cancellationToken);
            return NoContent();
        }
        catch (AuthException ex)
        {
            return AuthError(ex);
        }
    }

    [HttpDelete("api/v1/admin/assignments")]
    public async Task<IActionResult> Unassign([FromBody] AssignmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await users.UnassignAsync(request, cancellationToken);
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
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
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
