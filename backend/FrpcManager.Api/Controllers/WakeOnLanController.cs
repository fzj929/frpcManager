using FrpcManager.Api.DTOs;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Sockets;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/wake-on-lan")]
[Authorize]
public class WakeOnLanController : ControllerBase
{
    private const string DefaultBroadcastAddress = "255.255.255.255";
    private readonly WakeOnLanService _wakeOnLanService;
    private readonly AuditLogService _auditLogService;

    public WakeOnLanController(WakeOnLanService wakeOnLanService, AuditLogService auditLogService)
    {
        _wakeOnLanService = wakeOnLanService;
        _auditLogService = auditLogService;
    }

    [HttpPost]
    public async Task<IActionResult> Wake([FromBody] WakeOnLanRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MacAddress))
            return BadRequest(new { message = "请输入 MAC 地址" });

        var broadcastAddress = string.IsNullOrWhiteSpace(request.BroadcastAddress)
            ? DefaultBroadcastAddress
            : request.BroadcastAddress.Trim();
        var port = request.Port == 0 ? 9 : request.Port;

        try
        {
            await _wakeOnLanService.SendMagicPacketAsync(request.MacAddress, broadcastAddress, port);
            await _auditLogService.LogAsync(HttpContext, "wake-on-lan.send", request.MacAddress, $"{broadcastAddress}:{port}");
            return Ok(new WakeOnLanResponse(
                request.MacAddress,
                broadcastAddress,
                port,
                "魔术数据包已发送"
            ));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (SocketException ex)
        {
            return StatusCode(500, new { message = $"发送魔术数据包失败：{ex.Message}" });
        }
    }
}
