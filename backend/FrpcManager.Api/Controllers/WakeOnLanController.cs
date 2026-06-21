using System.Security.Claims;
using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _db;

    public WakeOnLanController(WakeOnLanService wakeOnLanService, AuditLogService auditLogService, AppDbContext db)
    {
        _wakeOnLanService = wakeOnLanService;
        _auditLogService = auditLogService;
        _db = db;
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
            await AddWakeLogAsync(request.MacAddress, broadcastAddress, port, "manual", true, "魔术数据包已发送");
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
            await AddWakeLogAsync(request.MacAddress, broadcastAddress, port, "manual", false, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (SocketException ex)
        {
            await AddWakeLogAsync(request.MacAddress, broadcastAddress, port, "manual", false, ex.Message);
            return StatusCode(500, new { message = $"发送魔术数据包失败：{ex.Message}" });
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var logs = await _db.WakeLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new WakeLogResponse(
                l.Id,
                l.MacAddress,
                l.BroadcastAddress,
                l.Port,
                l.Source,
                l.Username,
                l.IpAddress,
                l.Success,
                l.Message,
                l.CreatedAt))
            .ToListAsync();

        return Ok(logs);
    }

    [HttpPost("logs/{id:int}/wake")]
    public async Task<IActionResult> WakeFromLog(int id)
    {
        var log = await _db.WakeLogs.FindAsync(id);
        if (log == null)
            return NotFound(new { message = "唤醒记录不存在" });

        return await Wake(new WakeOnLanRequest(log.MacAddress, log.BroadcastAddress, log.Port));
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> Schedules()
    {
        var schedules = await _db.WakeSchedules
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new WakeScheduleResponse(
                s.Id,
                s.Name,
                s.MacAddress,
                s.BroadcastAddress,
                s.Port,
                s.TimeOfDay,
                s.IsEnabled,
                s.LastRunAt,
                s.CreatedAt,
                s.UpdatedAt))
            .ToListAsync();

        return Ok(schedules);
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] WakeScheduleRequest request)
    {
        var validation = ValidateScheduleRequest(request, out var broadcastAddress, out var timeOfDay);
        if (validation != null) return validation;

        var schedule = new WakeSchedule
        {
            Name = request.Name.Trim(),
            MacAddress = request.MacAddress.Trim(),
            BroadcastAddress = broadcastAddress,
            Port = request.Port == 0 ? 9 : request.Port,
            TimeOfDay = timeOfDay,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _db.WakeSchedules.Add(schedule);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.schedule.create", schedule.MacAddress, schedule.Name);
        return Ok(ToScheduleResponse(schedule));
    }

    [HttpPut("schedules/{id:int}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] WakeScheduleRequest request)
    {
        var schedule = await _db.WakeSchedules.FindAsync(id);
        if (schedule == null)
            return NotFound(new { message = "定时任务不存在" });

        var validation = ValidateScheduleRequest(request, out var broadcastAddress, out var timeOfDay);
        if (validation != null) return validation;

        schedule.Name = request.Name.Trim();
        schedule.MacAddress = request.MacAddress.Trim();
        schedule.BroadcastAddress = broadcastAddress;
        schedule.Port = request.Port == 0 ? 9 : request.Port;
        schedule.TimeOfDay = timeOfDay;
        schedule.IsEnabled = request.IsEnabled;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.schedule.update", schedule.MacAddress, schedule.Name);
        return Ok(ToScheduleResponse(schedule));
    }

    [HttpDelete("schedules/{id:int}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var schedule = await _db.WakeSchedules.FindAsync(id);
        if (schedule == null)
            return NotFound(new { message = "定时任务不存在" });

        _db.WakeSchedules.Remove(schedule);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.schedule.delete", schedule.MacAddress, schedule.Name);
        return Ok(new { message = "定时任务已删除" });
    }

    [HttpPost("schedules/{id:int}/wake")]
    public async Task<IActionResult> WakeFromSchedule(int id)
    {
        var schedule = await _db.WakeSchedules.FindAsync(id);
        if (schedule == null)
            return NotFound(new { message = "定时任务不存在" });

        return await Wake(new WakeOnLanRequest(schedule.MacAddress, schedule.BroadcastAddress, schedule.Port));
    }

    private async Task AddWakeLogAsync(string macAddress, string broadcastAddress, int port, string source, bool success, string message)
    {
        _db.WakeLogs.Add(new WakeLog
        {
            MacAddress = macAddress.Trim(),
            BroadcastAddress = broadcastAddress,
            Port = port,
            Source = source,
            Username = HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous",
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            Success = success,
            Message = message,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    private BadRequestObjectResult? ValidateScheduleRequest(WakeScheduleRequest request, out string broadcastAddress, out string timeOfDay)
    {
        broadcastAddress = string.IsNullOrWhiteSpace(request.BroadcastAddress)
            ? DefaultBroadcastAddress
            : request.BroadcastAddress.Trim();
        timeOfDay = request.TimeOfDay.Trim();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "请输入任务名称" });

        if (string.IsNullOrWhiteSpace(request.MacAddress))
            return BadRequest(new { message = "请输入 MAC 地址" });

        if (request.Port is < 0 or > 65535)
            return BadRequest(new { message = "端口必须在 1-65535 之间" });

        if (!TimeOnly.TryParse(timeOfDay, out _))
            return BadRequest(new { message = "时间格式不正确" });

        return null;
    }

    private static WakeLogResponse ToLogResponse(WakeLog log) => new(
        log.Id,
        log.MacAddress,
        log.BroadcastAddress,
        log.Port,
        log.Source,
        log.Username,
        log.IpAddress,
        log.Success,
        log.Message,
        log.CreatedAt);

    private static WakeScheduleResponse ToScheduleResponse(WakeSchedule schedule) => new(
        schedule.Id,
        schedule.Name,
        schedule.MacAddress,
        schedule.BroadcastAddress,
        schedule.Port,
        schedule.TimeOfDay,
        schedule.IsEnabled,
        schedule.LastRunAt,
        schedule.CreatedAt,
        schedule.UpdatedAt);
}
