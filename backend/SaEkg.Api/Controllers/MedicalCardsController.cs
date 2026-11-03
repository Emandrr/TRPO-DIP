using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Api.Contracts;
using SaEkg.Api.Middleware;
using SaEkg.Application.Ecg;
using SaEkg.Application.Medical;
using SaEkg.Domain.Enums;

namespace SaEkg.Api.Controllers;

[ApiController]
[Route("api/v1/medical-cards")]
[Authorize]
public sealed class MedicalCardsController(IMedicalDataService medical) : ControllerBase
{
    [HttpGet("{patientId:guid}")]
    public async Task<IActionResult> Get(Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var card = await medical.GetMedicalCardAsync(patientId, userId, role, cancellationToken);
            return card is null ? NotFound() : Ok(card);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPut("{patientId:guid}")]
    [Authorize(Roles = UserRole.Doctor)]
    public async Task<IActionResult> Upsert(Guid patientId, [FromBody] UpsertMedicalCardRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var card = await medical.UpsertMedicalCardAsync(patientId, request, userId, role, cancellationToken);
            return Ok(card);
        }
        catch (MedValidationException ex)
        {
            return ValidationError(ex);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpDelete("{patientId:guid}")]
    [Authorize(Roles = $"{UserRole.Doctor},{UserRole.Admin}")]
    public async Task<IActionResult> Delete(Guid patientId, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            await medical.SoftDeleteMedicalCardAsync(patientId, userId, role, cancellationToken);
            return NoContent();
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    private bool TryGetUser(out Guid userId, out string role)
    {
        role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private ObjectResult ValidationError(MedValidationException ex) =>
        StatusCode(StatusCodes.Status422UnprocessableEntity, new ApiError
        {
            Code = ErrorCodes.ValidationError,
            Message = ex.Message,
            Details = new { errors = ex.Errors },
            RequestId = RequestIdMiddleware.GetRequestId(HttpContext)
        });

    private ObjectResult MapError(EcgUploadException ex)
    {
        var status = ex.Code switch
        {
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
            ErrorCodes.ValidationError => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
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
