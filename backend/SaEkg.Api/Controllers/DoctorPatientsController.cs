using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Api.Contracts;
using SaEkg.Api.Middleware;
using SaEkg.Application.Auth;
using SaEkg.Application.Ecg;
using SaEkg.Domain.Enums;

namespace SaEkg.Api.Controllers;

[ApiController]
[Authorize]
public sealed class DoctorPatientsController(IDoctorPatientQueryService patients) : ControllerBase
{
    [HttpGet("api/v1/doctor/patients")]
    [Authorize(Roles = UserRole.Doctor)]
    public async Task<IActionResult> MyPatients(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var doctorId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await patients.ListPatientsAsync(doctorId, doctorId, UserRole.Doctor, cancellationToken));
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    [HttpGet("api/v1/admin/doctors/{doctorId:guid}/patients")]
    [Authorize(Roles = UserRole.Admin)]
    public async Task<IActionResult> AdminList(Guid doctorId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await patients.ListPatientsAsync(doctorId, adminId, UserRole.Admin, cancellationToken));
        }
        catch (EcgUploadException ex)
        {
            return MapError(ex);
        }
    }

    private ObjectResult MapError(EcgUploadException ex)
    {
        var status = ex.Code switch
        {
            ErrorCodes.Forbidden => StatusCodes.Status403Forbidden,
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
}
