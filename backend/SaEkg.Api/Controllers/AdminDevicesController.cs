using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Application.Devices;
using SaEkg.Domain.Enums;

namespace SaEkg.Api.Controllers;

[ApiController]
[Route("api/v1/admin/devices")]
[Authorize(Roles = UserRole.Admin)]
public sealed class AdminDevicesController(IDeviceService devices) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> List(CancellationToken cancellationToken)
        => Ok(await devices.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DeviceDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await devices.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Create([FromBody] CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return BadRequest();
        }

        var created = await devices.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DeviceDto>> Update(Guid id, [FromBody] UpdateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest();
        }

        var updated = await devices.UpdateAsync(id, request, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : Guid.Empty;
        var deleted = await devices.SoftDeleteAsync(id, userId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
