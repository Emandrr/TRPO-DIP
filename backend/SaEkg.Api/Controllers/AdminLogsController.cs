using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaEkg.Application.Logging;
using SaEkg.Domain.Enums;

namespace SaEkg.Api.Controllers;

[ApiController]
[Route("api/v1/admin/logs")]
[Authorize(Roles = UserRole.Admin)]
public sealed class AdminLogsController(ISystemLogQueryService logs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SystemLogDto>>> Get(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? level,
        [FromQuery] string? module,
        [FromQuery] string? code,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await logs.QueryAsync(
            new SystemLogQuery(from, to, level, module, code, page, pageSize),
            cancellationToken);
        return Ok(result);
    }
}
