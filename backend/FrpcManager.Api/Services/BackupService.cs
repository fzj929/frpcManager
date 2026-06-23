using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class BackupService
{
    private readonly AppDbContext _db;
    private readonly FrpcApiService _frpcApi;
    private readonly HttpsProxyRuntimeService _httpsProxyRuntime;

    public BackupService(AppDbContext db, FrpcApiService frpcApi, HttpsProxyRuntimeService httpsProxyRuntime)
    {
        _db = db;
        _frpcApi = frpcApi;
        _httpsProxyRuntime = httpsProxyRuntime;
    }

    public async Task<BackupResponse> ExportAsync()
    {
        var proxies = await _db.Proxies
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Type)
            .Select(p => new BackupProxyItem(
                p.Name,
                p.Type,
                p.LocalIP,
                p.LocalPort,
                p.RemotePort,
                p.Description,
                p.IsEnabled,
                p.ExpiresAt
            ))
            .ToListAsync();

        var httpsProxies = await _db.HttpsProxyRules
            .OrderBy(r => r.Name)
            .Select(r => new BackupHttpsProxyItem(
                r.Name,
                r.ListenPort,
                r.TargetUrl,
                r.CertificateMode,
                r.Description,
                r.IsEnabled
            ))
            .ToListAsync();

        var wakeMacAddresses = await _db.WakeMacAddresses
            .OrderBy(m => m.Name)
            .ThenBy(m => m.MacAddress)
            .Select(m => new BackupWakeMacAddressItem(
                m.MacAddress,
                m.Name
            ))
            .ToListAsync();

        var wakeSchedules = await _db.WakeSchedules
            .OrderBy(s => s.Name)
            .ThenBy(s => s.MacAddress)
            .Select(s => new BackupWakeScheduleItem(
                s.Name,
                s.MacAddress,
                s.BroadcastAddress,
                s.Port,
                s.TimeOfDay,
                s.ScheduleMode,
                s.DaysOfWeek,
                s.SpecificDate,
                s.IsEnabled,
                s.LastRunAt
            ))
            .ToListAsync();

        var frpcConfig = await _frpcApi.GetConfigAsync();
        return new BackupResponse("4", DateTime.UtcNow, proxies, httpsProxies, wakeMacAddresses, wakeSchedules, frpcConfig);
    }

    public async Task RestoreAsync(RestoreRequest request)
    {
        foreach (var item in request.WakeMacAddresses ?? [])
        {
            var macAddress = WakeOnLanService.NormalizeMacAddress(item.MacAddress);
            var existing = await _db.WakeMacAddresses.FirstOrDefaultAsync(m => m.MacAddress == macAddress);

            if (existing == null)
            {
                existing = new WakeMacAddress { CreatedAt = DateTime.UtcNow };
                _db.WakeMacAddresses.Add(existing);
            }

            existing.MacAddress = macAddress;
            existing.Name = string.IsNullOrWhiteSpace(item.Name) ? macAddress : item.Name.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var item in request.WakeSchedules ?? [])
        {
            var macAddress = WakeOnLanService.NormalizeMacAddress(item.MacAddress);
            var existing = await _db.WakeSchedules.FirstOrDefaultAsync(s =>
                s.Name == item.Name && s.MacAddress == macAddress);

            if (existing == null)
            {
                existing = new WakeSchedule { CreatedAt = DateTime.UtcNow };
                _db.WakeSchedules.Add(existing);
            }

            existing.Name = item.Name;
            existing.MacAddress = macAddress;
            existing.BroadcastAddress = string.IsNullOrWhiteSpace(item.BroadcastAddress) ? "255.255.255.255" : item.BroadcastAddress.Trim();
            existing.Port = item.Port == 0 ? 9 : item.Port;
            existing.TimeOfDay = string.IsNullOrWhiteSpace(item.TimeOfDay) ? "08:00" : item.TimeOfDay.Trim();
            existing.ScheduleMode = NormalizeScheduleMode(item.ScheduleMode);
            existing.DaysOfWeek = NormalizeDaysOfWeek(item.DaysOfWeek);
            existing.SpecificDate = item.SpecificDate?.Date;
            existing.IsEnabled = item.IsEnabled;
            existing.LastRunAt = item.LastRunAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var item in request.Proxies ?? [])
        {
            var itemType = item.Type.ToLowerInvariant();
            var proxy = await _db.Proxies.FirstOrDefaultAsync(p => p.Name == item.Name && p.Type == itemType);

            if (proxy == null)
            {
                proxy = new Proxy { CreatedAt = DateTime.UtcNow };
                _db.Proxies.Add(proxy);
            }

            proxy.Name = item.Name;
            proxy.Type = itemType;
            proxy.LocalIP = item.LocalIP;
            proxy.LocalPort = item.LocalPort;
            proxy.RemotePort = item.RemotePort;
            proxy.Description = item.Description;
            proxy.IsEnabled = item.IsEnabled;
            proxy.ExpiresAt = item.ExpiresAt;
            proxy.UpdatedAt = DateTime.UtcNow;
        }

        var restoredHttpsRules = new List<HttpsProxyRule>();
        foreach (var item in request.HttpsProxies ?? [])
        {
            var rule = await _db.HttpsProxyRules.FirstOrDefaultAsync(r => r.ListenPort == item.ListenPort);

            if (rule != null)
                await _httpsProxyRuntime.StopAsync(rule.Id);

            if (rule == null)
            {
                rule = new HttpsProxyRule { CreatedAt = DateTime.UtcNow };
                _db.HttpsProxyRules.Add(rule);
            }

            var usedUploadedCertificate = item.CertificateMode is "pfx" or "pem" or "uploaded";
            rule.Name = item.Name;
            rule.ListenPort = item.ListenPort;
            rule.TargetUrl = item.TargetUrl;
            rule.CertificateMode = "default";
            rule.CertificatePath = "";
            rule.CertificateKeyPath = "";
            rule.CertificatePassword = "";
            rule.Description = item.Description;
            rule.IsEnabled = item.IsEnabled && !usedUploadedCertificate;
            rule.UpdatedAt = DateTime.UtcNow;
            restoredHttpsRules.Add(rule);
        }

        await _db.SaveChangesAsync();

        foreach (var rule in restoredHttpsRules.Where(r => r.IsEnabled))
            await _httpsProxyRuntime.RestartAsync(rule);

        if (request.ApplyFrpcConfig && request.FrpcConfig != null)
        {
            await _frpcApi.PutConfigAsync(request.FrpcConfig);
            await _frpcApi.ReloadAsync();
        }
    }

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
}
