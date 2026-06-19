using FrpcManager.Api.DTOs;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProxiesController : ControllerBase
{
    private readonly ProxyService _proxyService;
    private readonly AuditLogService _auditLogService;

    public ProxiesController(ProxyService proxyService, AuditLogService auditLogService)
    {
        _proxyService = proxyService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _proxyService.GetAllProxiesAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProxyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "通道名称不能为空" });

        var result = await _proxyService.CreateProxyAsync(request);
        await _auditLogService.LogAsync(HttpContext, "proxy.create", request.Name, $"{request.Type}:{request.LocalIP}:{request.LocalPort}->{request.RemotePort}");
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProxyRequest request)
    {
        var result = await _proxyService.UpdateProxyAsync(id, request);
        if (result == null) return NotFound(new { message = "通道不存在" });

        await _auditLogService.LogAsync(HttpContext, "proxy.update", request.Name, $"id={id}");
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _proxyService.DeleteProxyAsync(id);
        if (!success) return NotFound(new { message = "通道不存在" });

        await _auditLogService.LogAsync(HttpContext, "proxy.delete", id.ToString());
        return Ok(new { message = "删除成功" });
    }

    [HttpPut("{id:int}/enable")]
    public async Task<IActionResult> Enable(int id, [FromBody] EnableRequest request)
    {
        var (success, message) = await _proxyService.SetEnabledAsync(id, true, request.DurationMinutes);
        if (!success) return BadRequest(new { message });

        await _auditLogService.LogAsync(HttpContext, "proxy.enable", id.ToString(), $"durationMinutes={request.DurationMinutes?.ToString() ?? "none"}");
        var tip = request.DurationMinutes.HasValue
            ? $"通道已启用，将在 {FormatDuration(request.DurationMinutes.Value)} 后自动关闭"
            : "通道已启用（无时间限制）";
        return Ok(new { message = tip });
    }

    [HttpPut("{id:int}/disable")]
    public async Task<IActionResult> Disable(int id)
    {
        var (success, message) = await _proxyService.SetEnabledAsync(id, false);
        if (!success) return BadRequest(new { message });

        await _auditLogService.LogAsync(HttpContext, "proxy.disable", id.ToString());
        return Ok(new { message = "通道已停用" });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncFromFrpc()
    {
        await _proxyService.SyncFromFrpcAsync();
        await _auditLogService.LogAsync(HttpContext, "proxy.sync-from-frpc", "frpc");
        return Ok(new { message = "已从 frpc 同步配置" });
    }

    private static string FormatDuration(int minutes) => minutes switch
    {
        30 => "30 分钟",
        60 => "1 小时",
        120 => "2 小时",
        480 => "8 小时",
        720 => "12 小时",
        _ => $"{minutes} 分钟"
    };
}
