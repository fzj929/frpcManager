using System.Security.Claims;
using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;
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
        return await WakeCore(request, "manual");
    }

    [HttpPost("ping")]
    public async Task<IActionResult> Ping([FromBody] WakePingRequest? request)
    {
        if (request == null)
            return BadRequest(new { message = "请输入要测试的 IP 或域名" });

        string host;
        try
        {
            host = WakeOnLanService.NormalizePingHost(request.Host);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        try
        {
            var reply = await _wakeOnLanService.PingAsync(host, request.TimeoutMs);
            var online = reply.Status == IPStatus.Success;
            var message = online
                ? $"主机在线，延迟 {reply.RoundtripTime} ms"
                : $"未收到响应：{reply.Status}";

            await _auditLogService.LogAsync(HttpContext, "wake-on-lan.ping", host, reply.Status.ToString(), online);
            return Ok(new WakePingResponse(
                host,
                online,
                online ? reply.RoundtripTime : null,
                reply.Status.ToString(),
                message));
        }
        catch (PingException ex)
        {
            await _auditLogService.LogAsync(HttpContext, "wake-on-lan.ping", host, ex.Message, false);
            return Ok(new WakePingResponse(host, false, null, "Error", $"Ping 测试失败：{ex.Message}"));
        }
    }

    private async Task<IActionResult> WakeCore(WakeOnLanRequest? request, string source)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.MacAddress))
            return BadRequest(new { message = "请输入 MAC 地址" });

        string macAddress;
        try
        {
            macAddress = WakeOnLanService.NormalizeMacAddress(request.MacAddress);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var broadcastAddress = string.IsNullOrWhiteSpace(request.BroadcastAddress)
            ? DefaultBroadcastAddress
            : request.BroadcastAddress.Trim();
        var port = request.Port == 0 ? 9 : request.Port;

        try
        {
            await EnsureMacAddressAsync(macAddress);
            await _wakeOnLanService.SendMagicPacketAsync(macAddress, broadcastAddress, port);
            await AddWakeLogAsync(macAddress, broadcastAddress, port, source, true, "魔术数据包已发送");
            await _auditLogService.LogAsync(HttpContext, "wake-on-lan.send", macAddress, $"{broadcastAddress}:{port}");
            return Ok(new WakeOnLanResponse(
                macAddress,
                broadcastAddress,
                port,
                "魔术数据包已发送"
            ));
        }
        catch (ArgumentException ex)
        {
            await AddWakeLogAsync(macAddress, broadcastAddress, port, source, false, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (SocketException ex)
        {
            await AddWakeLogAsync(macAddress, broadcastAddress, port, source, false, ex.Message);
            return StatusCode(500, new { message = $"发送魔术数据包失败：{ex.Message}" });
        }
    }

    [HttpGet("mac-addresses")]
    public async Task<IActionResult> MacAddresses()
    {
        var items = await _db.WakeMacAddresses
            .OrderBy(m => m.Name)
            .ThenBy(m => m.MacAddress)
            .Select(m => new WakeMacAddressResponse(
                m.Id,
                m.MacAddress,
                m.Name,
                m.CreatedAt,
                m.UpdatedAt))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("mac-addresses")]
    public async Task<IActionResult> CreateMacAddress([FromBody] WakeMacAddressRequest request)
    {
        var validation = ValidateMacAddressRequest(request, out var macAddress, out var name);
        if (validation != null) return validation;

        var exists = await _db.WakeMacAddresses.AnyAsync(m => m.MacAddress == macAddress);
        if (exists)
            return BadRequest(new { message = "MAC 地址已存在" });

        var item = new WakeMacAddress
        {
            MacAddress = macAddress,
            Name = name,
            CreatedAt = DateTime.UtcNow
        };

        _db.WakeMacAddresses.Add(item);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.mac.create", macAddress, name);
        return Ok(ToMacAddressResponse(item));
    }

    [HttpPut("mac-addresses/{id:int}")]
    public async Task<IActionResult> UpdateMacAddress(int id, [FromBody] WakeMacAddressRequest request)
    {
        var item = await _db.WakeMacAddresses.FindAsync(id);
        if (item == null)
            return NotFound(new { message = "MAC 地址不存在" });

        var validation = ValidateMacAddressRequest(request, out var macAddress, out var name);
        if (validation != null) return validation;

        var exists = await _db.WakeMacAddresses.AnyAsync(m => m.Id != id && m.MacAddress == macAddress);
        if (exists)
            return BadRequest(new { message = "MAC 地址已存在" });

        item.MacAddress = macAddress;
        item.Name = name;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.mac.update", macAddress, name);
        return Ok(ToMacAddressResponse(item));
    }

    [HttpDelete("mac-addresses/{id:int}")]
    public async Task<IActionResult> DeleteMacAddress(int id)
    {
        var item = await _db.WakeMacAddresses.FindAsync(id);
        if (item == null)
            return NotFound(new { message = "MAC 地址不存在" });

        _db.WakeMacAddresses.Remove(item);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.mac.delete", item.MacAddress, item.Name);
        return Ok(new { message = "MAC 地址已删除" });
    }

    [HttpGet("logs")]
    public async Task<IActionResult> Logs([FromQuery] int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var logs = await _db.WakeLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
        var nameMap = await GetMacNameMapAsync(logs.Select(l => l.MacAddress));
        var response = logs.Select(l => ToLogResponse(l, nameMap)).ToList();

        return Ok(response);
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
            .ToListAsync();
        var nameMap = await GetMacNameMapAsync(schedules.Select(s => s.MacAddress));
        var response = schedules.Select(s => ToScheduleResponse(s, nameMap)).ToList();

        return Ok(response);
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] WakeScheduleRequest request)
    {
        var validation = ValidateScheduleRequest(request, out var macAddress, out var broadcastAddress, out var timeOfDay);
        if (validation != null) return validation;

        var schedule = new WakeSchedule
        {
            Name = request.Name.Trim(),
            MacAddress = macAddress,
            BroadcastAddress = broadcastAddress,
            Port = request.Port == 0 ? 9 : request.Port,
            TimeOfDay = timeOfDay,
            ScheduleMode = NormalizeScheduleMode(request.ScheduleMode),
            DaysOfWeek = NormalizeDaysOfWeek(request.DaysOfWeek),
            SpecificDate = NormalizeSpecificDate(request.SpecificDate),
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };

        _db.WakeSchedules.Add(schedule);
        await EnsureMacAddressAsync(schedule.MacAddress);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.schedule.create", schedule.MacAddress, schedule.Name);
        var nameMap = await GetMacNameMapAsync([schedule.MacAddress]);
        return Ok(ToScheduleResponse(schedule, nameMap));
    }

    [HttpPut("schedules/{id:int}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] WakeScheduleRequest request)
    {
        var schedule = await _db.WakeSchedules.FindAsync(id);
        if (schedule == null)
            return NotFound(new { message = "定时任务不存在" });

        var validation = ValidateScheduleRequest(request, out var macAddress, out var broadcastAddress, out var timeOfDay);
        if (validation != null) return validation;

        schedule.Name = request.Name.Trim();
        schedule.MacAddress = macAddress;
        schedule.BroadcastAddress = broadcastAddress;
        schedule.Port = request.Port == 0 ? 9 : request.Port;
        schedule.TimeOfDay = timeOfDay;
        schedule.ScheduleMode = NormalizeScheduleMode(request.ScheduleMode);
        schedule.DaysOfWeek = NormalizeDaysOfWeek(request.DaysOfWeek);
        schedule.SpecificDate = NormalizeSpecificDate(request.SpecificDate);
        schedule.IsEnabled = request.IsEnabled;
        schedule.UpdatedAt = DateTime.UtcNow;

        await EnsureMacAddressAsync(schedule.MacAddress);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "wake-on-lan.schedule.update", schedule.MacAddress, schedule.Name);
        var nameMap = await GetMacNameMapAsync([schedule.MacAddress]);
        return Ok(ToScheduleResponse(schedule, nameMap));
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

        return await WakeCore(new WakeOnLanRequest(schedule.MacAddress, schedule.BroadcastAddress, schedule.Port), "schedule");
    }

    private async Task AddWakeLogAsync(string macAddress, string broadcastAddress, int port, string source, bool success, string message)
    {
        await EnsureMacAddressAsync(macAddress);
        _db.WakeLogs.Add(new WakeLog
        {
            MacAddress = WakeOnLanService.NormalizeMacAddress(macAddress),
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

    private BadRequestObjectResult? ValidateScheduleRequest(WakeScheduleRequest request, out string macAddress, out string broadcastAddress, out string timeOfDay)
    {
        macAddress = "";
        broadcastAddress = string.IsNullOrWhiteSpace(request.BroadcastAddress)
            ? DefaultBroadcastAddress
            : request.BroadcastAddress.Trim();
        timeOfDay = request.TimeOfDay.Trim();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "请输入任务名称" });

        if (string.IsNullOrWhiteSpace(request.MacAddress))
            return BadRequest(new { message = "请输入 MAC 地址" });

        try
        {
            macAddress = WakeOnLanService.NormalizeMacAddress(request.MacAddress);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (request.Port is < 0 or > 65535)
            return BadRequest(new { message = "端口必须在 1-65535 之间" });

        if (!TimeOnly.TryParse(timeOfDay, out _))
            return BadRequest(new { message = "时间格式不正确" });

        var scheduleMode = NormalizeScheduleMode(request.ScheduleMode);
        if (scheduleMode == "weekly" && string.IsNullOrWhiteSpace(NormalizeDaysOfWeek(request.DaysOfWeek)))
            return BadRequest(new { message = "请选择每周执行的日期" });

        if (scheduleMode == "date" && !request.SpecificDate.HasValue)
            return BadRequest(new { message = "请选择指定日期" });

        return null;
    }

    private BadRequestObjectResult? ValidateMacAddressRequest(WakeMacAddressRequest request, out string macAddress, out string name)
    {
        macAddress = "";
        name = "";

        if (request == null || string.IsNullOrWhiteSpace(request.MacAddress))
            return BadRequest(new { message = "请输入 MAC 地址" });

        try
        {
            macAddress = WakeOnLanService.NormalizeMacAddress(request.MacAddress);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        name = string.IsNullOrWhiteSpace(request.Name) ? macAddress : request.Name.Trim();
        if (name.Length > 100)
            return BadRequest(new { message = "名称不能超过 100 个字符" });

        return null;
    }

    private async Task EnsureMacAddressAsync(string macAddress)
    {
        var normalized = WakeOnLanService.NormalizeMacAddress(macAddress);
        if (_db.WakeMacAddresses.Local.Any(m => m.MacAddress == normalized))
            return;

        var exists = await _db.WakeMacAddresses.AnyAsync(m => m.MacAddress == normalized);
        if (exists) return;

        _db.WakeMacAddresses.Add(new WakeMacAddress
        {
            MacAddress = normalized,
            Name = normalized,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task<Dictionary<string, string>> GetMacNameMapAsync(IEnumerable<string> macAddresses)
    {
        var normalized = macAddresses
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(WakeOnLanService.NormalizeMacAddress)
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
            return new Dictionary<string, string>();

        return await _db.WakeMacAddresses
            .Where(m => normalized.Contains(m.MacAddress))
            .ToDictionaryAsync(m => m.MacAddress, m => m.Name);
    }

    private static WakeLogResponse ToLogResponse(WakeLog log, Dictionary<string, string> nameMap) => new(
        log.Id,
        log.MacAddress,
        nameMap.GetValueOrDefault(log.MacAddress, log.MacAddress),
        log.BroadcastAddress,
        log.Port,
        log.Source,
        log.Username,
        log.IpAddress,
        log.Success,
        log.Message,
        log.CreatedAt);

    private static WakeScheduleResponse ToScheduleResponse(WakeSchedule schedule, Dictionary<string, string> nameMap) => new(
        schedule.Id,
        schedule.Name,
        schedule.MacAddress,
        nameMap.GetValueOrDefault(schedule.MacAddress, schedule.MacAddress),
        schedule.BroadcastAddress,
        schedule.Port,
        schedule.TimeOfDay,
        schedule.ScheduleMode,
        schedule.DaysOfWeek,
        schedule.SpecificDate,
        schedule.IsEnabled,
        schedule.LastRunAt,
        schedule.CreatedAt,
        schedule.UpdatedAt);

    private static string NormalizeScheduleMode(string? scheduleMode)
    {
        return scheduleMode switch
        {
            "weekly" => "weekly",
            "date" => "date",
            _ => "daily"
        };
    }

    private static string NormalizeDaysOfWeek(string? daysOfWeek)
    {
        if (string.IsNullOrWhiteSpace(daysOfWeek))
            return "";

        var values = daysOfWeek
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => int.TryParse(v, out var day) ? day : -1)
            .Where(day => day is >= 0 and <= 6)
            .Distinct()
            .OrderBy(day => day)
            .Select(day => day.ToString());

        return string.Join(",", values);
    }

    private static DateTime? NormalizeSpecificDate(DateTime? specificDate)
    {
        return specificDate?.Date;
    }

    private static WakeMacAddressResponse ToMacAddressResponse(WakeMacAddress item) => new(
        item.Id,
        item.MacAddress,
        item.Name,
        item.CreatedAt,
        item.UpdatedAt);
}
