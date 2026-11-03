using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Api.Contracts;
using SaEkg.Api.Middleware;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Constants;
using SaEkg.Domain.Enums;

namespace SaEkg.Api.Controllers;

[ApiController]
[Route("api/v1/ecg-records")]
[Authorize]
public sealed class EcgRecordsController(
    IEcgUploadService uploads,
    IEcgReportService reports,
    IEcgAnalysisService analysis) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = UserRole.Patient)]
    [RequestSizeLimit(CsvValidationConstants.MaxFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = CsvValidationConstants.MaxFileBytes)]
    public async Task<IActionResult> Upload(
        [FromForm] Guid deviceId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Unprocessable(new ApiError
            {
                Code = ErrorCodes.CsvCheckFailed,
                Message = "Файл не передан.",
                Details = new { check = 1, line = (int?)null },
                RequestId = RequestIdMiddleware.GetRequestId(HttpContext)
            });
        }

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var patientId))
        {
            return Unauthorized();
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await uploads.UploadAsync(
                patientId,
                deviceId,
                file.FileName,
                stream,
                file.Length,
                cancellationToken);
            return Accepted(result);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EcgRecordDto>>> List(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? uploadedFrom,
        [FromQuery] DateTimeOffset? uploadedTo,
        CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        return Ok(await uploads.ListForUserAsync(userId, role, status, uploadedFrom, uploadedTo, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EcgRecordDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var item = await uploads.GetForUserAsync(id, userId, role, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("{id:guid}/signal")]
    public async Task<IActionResult> Signal(
        Guid id,
        [FromQuery] long fromMs,
        [FromQuery] long toMs,
        [FromQuery] int maxPoints = 2000,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var points = await reports.GetSignalAsync(id, userId, role, fromMs, toMs, maxPoints, cancellationToken);
            if (points.Count == 0 && role == UserRole.Patient)
            {
                return NotFound();
            }

            return Ok(points);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("{id:guid}/report")]
    public async Task<IActionResult> Report(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUser(out var userId, out var role))
        {
            return Unauthorized();
        }

        try
        {
            var report = await reports.GetReportAsync(id, userId, role, cancellationToken);
            return report is null ? NotFound() : Ok(report);
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("{id:guid}/report/accept")]
    [Authorize(Roles = UserRole.Doctor)]
    public async Task<IActionResult> Accept(Guid id, [FromBody] DecisionRequest? body, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var doctorId))
        {
            return Unauthorized();
        }

        try
        {
            await reports.AcceptAsync(id, doctorId, body?.Comment, cancellationToken);
            return NoContent();
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("{id:guid}/report/reject")]
    [Authorize(Roles = UserRole.Doctor)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionRequest? body, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var doctorId))
        {
            return Unauthorized();
        }

        try
        {
            await reports.RejectAsync(id, doctorId, body?.Comment, cancellationToken);
            return NoContent();
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpPost("{id:guid}/reanalyze")]
    [Authorize(Roles = $"{UserRole.Doctor},{UserRole.Admin}")]
    public async Task<IActionResult> Reanalyze(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await analysis.ReanalyzeAsync(id, cancellationToken);
            return Accepted();
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

    private ObjectResult MapError(EcgUploadException ex)
    {
        var status = ex.Code switch
        {
            ErrorCodes.ConsentRequired => StatusCodes.Status403Forbidden,
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
            ErrorCodes.CsvCheckFailed => StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.ValidationError => StatusCodes.Status422UnprocessableEntity,
            ErrorCodes.NotFound => StatusCodes.Status404NotFound,
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

    private ObjectResult Unprocessable(ApiError error) =>
        StatusCode(StatusCodes.Status422UnprocessableEntity, error);
}

public sealed record DecisionRequest(string? Comment);
