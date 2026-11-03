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
[Authorize]
public sealed class RecommendationsController(IMedicalDataService medical) : ControllerBase
{
    [HttpGet("api/v1/diagnoses/{diagnosisId:guid}/recommendations")]
    public async Task<IActionResult> List(Guid diagnosisId, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await medical.ListRecommendationsAsync(diagnosisId, userId, role, cancellationToken));
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("api/v1/recommendations/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var item = await medical.GetRecommendationAsync(id, userId, role, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("api/v1/diagnoses/{diagnosisId:guid}/recommendations")]
    [Authorize(Roles = UserRole.Doctor)]
    public async Task<IActionResult> Create(Guid diagnosisId, [FromBody] UpsertRecommendationRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var doctorId))
        {
            return Unauthorized();
        }

        try
        {
            var created = await medical.CreateRecommendationAsync(diagnosisId, request, doctorId, cancellationToken);
            return Created($"/api/v1/recommendations/{created.Id}", created);
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

    [HttpPut("api/v1/recommendations/{id:guid}")]
    [Authorize(Roles = UserRole.Doctor)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertRecommendationRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var doctorId))
        {
            return Unauthorized();
        }

        try
        {
            var updated = await medical.UpdateRecommendationAsync(id, request, doctorId, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
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

    [HttpDelete("api/v1/recommendations/{id:guid}")]
    [Authorize(Roles = $"{UserRole.Doctor},{UserRole.Admin}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            await medical.SoftDeleteRecommendationAsync(id, userId, role, cancellationToken);
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
