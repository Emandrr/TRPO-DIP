using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Application.Devices;

namespace SaEkg.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public sealed class DevicesController(IDeviceService devices) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> List(CancellationToken cancellationToken)
        => Ok(await devices.ListAsync(cancellationToken));
}
