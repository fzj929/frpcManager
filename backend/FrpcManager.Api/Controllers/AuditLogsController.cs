using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = UserRoles.Admin)]
public class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _auditLogService;

    public AuditLogsController(AuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 200)
    {
        return Ok(await _auditLogService.GetRecentAsync(limit));
    }
}
