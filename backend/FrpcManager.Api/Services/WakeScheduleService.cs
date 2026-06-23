using FrpcManager.Api.Data;
using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class WakeScheduleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WakeScheduleService> _logger;

    public WakeScheduleService(IServiceScopeFactory scopeFactory, ILogger<WakeScheduleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("定时唤醒服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueSchedulesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行定时唤醒任务失败");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RunDueSchedulesAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var wakeOnLan = scope.ServiceProvider.GetRequiredService<WakeOnLanService>();
        var now = DateTime.UtcNow;
        var localNow = DateTime.Now;
        var today = localNow.Date;

        var schedules = await db.WakeSchedules
            .Where(s => s.IsEnabled)
            .ToListAsync(stoppingToken);

        foreach (var schedule in schedules)
        {
            if (!TimeOnly.TryParse(schedule.TimeOfDay, out var timeOnly))
                continue;

            if (!ShouldRunToday(schedule, today))
                continue;

            var runAtToday = today.Add(timeOnly.ToTimeSpan());
            if (localNow < runAtToday || localNow >= runAtToday.AddSeconds(60))
                continue;

            if (schedule.LastRunAt.HasValue && schedule.LastRunAt.Value.ToLocalTime().Date == today)
                continue;

            var success = true;
            var message = "定时唤醒包已发送";
            var macAddress = WakeOnLanService.NormalizeMacAddress(schedule.MacAddress);
            try
            {
                await wakeOnLan.SendMagicPacketAsync(macAddress, schedule.BroadcastAddress, schedule.Port);
            }
            catch (Exception ex)
            {
                success = false;
                message = ex.Message;
            }

            schedule.LastRunAt = now;
            schedule.UpdatedAt = now;
            schedule.MacAddress = macAddress;
            if (schedule.ScheduleMode == "date")
                schedule.IsEnabled = false;

            if (!db.WakeMacAddresses.Local.Any(m => m.MacAddress == macAddress) &&
                !await db.WakeMacAddresses.AnyAsync(m => m.MacAddress == macAddress, stoppingToken))
            {
                db.WakeMacAddresses.Add(new WakeMacAddress
                {
                    MacAddress = macAddress,
                    Name = macAddress,
                    CreatedAt = now
                });
            }

            db.WakeLogs.Add(new WakeLog
            {
                MacAddress = macAddress,
                BroadcastAddress = schedule.BroadcastAddress,
                Port = schedule.Port,
                Source = "schedule",
                Username = "system",
                IpAddress = "",
                Success = success,
                Message = message,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(stoppingToken);
    }

    private static bool ShouldRunToday(WakeSchedule schedule, DateTime today)
    {
        return schedule.ScheduleMode switch
        {
            "date" => schedule.SpecificDate?.Date == today,
            "weekly" => IsWeeklyDaySelected(schedule.DaysOfWeek, today.DayOfWeek),
            _ => true
        };
    }

    private static bool IsWeeklyDaySelected(string daysOfWeek, DayOfWeek dayOfWeek)
    {
        if (string.IsNullOrWhiteSpace(daysOfWeek))
            return false;

        var today = ((int)dayOfWeek).ToString();
        return daysOfWeek
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(today);
    }
}
