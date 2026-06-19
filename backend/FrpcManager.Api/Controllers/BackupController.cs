using FrpcManager.Api.DTOs;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/backup")]
[Authorize]
public class BackupController : ControllerBase
{
    private readonly BackupService _backupService;
    private readonly AuditLogService _auditLogService;

    public BackupController(BackupService backupService, AuditLogService auditLogService)
    {
        _backupService = backupService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> Export()
    {
        var backup = await _backupService.ExportAsync();
        await _auditLogService.LogAsync(HttpContext, "backup.export", "configuration", $"proxies={backup.Proxies.Count}");
        return Ok(backup);
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreRequest request)
    {
        await _backupService.RestoreAsync(request);
        await _auditLogService.LogAsync(HttpContext, "backup.restore", "configuration", $"proxies={request.Proxies.Count}");
        return Ok(new { message = "配置已恢复" });
    }
}
