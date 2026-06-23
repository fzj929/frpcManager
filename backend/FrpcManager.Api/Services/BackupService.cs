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

        var frpcConfig = await _frpcApi.GetConfigAsync();
        return new BackupResponse("3", DateTime.UtcNow, proxies, httpsProxies, wakeMacAddresses, frpcConfig);
    }

    public async Task RestoreAsync(RestoreRequest request)
    {
        if (request.ReplaceExisting)
        {
            _db.Proxies.RemoveRange(_db.Proxies);
            _db.WakeMacAddresses.RemoveRange(_db.WakeMacAddresses);
            var existingHttpsRules = await _db.HttpsProxyRules.ToListAsync();
            foreach (var rule in existingHttpsRules)
                await _httpsProxyRuntime.StopAsync(rule.Id);

            _db.HttpsProxyRules.RemoveRange(existingHttpsRules);
        }

        foreach (var item in request.WakeMacAddresses ?? [])
        {
            var macAddress = WakeOnLanService.NormalizeMacAddress(item.MacAddress);
            var existing = request.ReplaceExisting
                ? null
                : await _db.WakeMacAddresses.FirstOrDefaultAsync(m => m.MacAddress == macAddress);

            if (existing == null)
            {
                existing = new WakeMacAddress { CreatedAt = DateTime.UtcNow };
                _db.WakeMacAddresses.Add(existing);
            }

            existing.MacAddress = macAddress;
            existing.Name = string.IsNullOrWhiteSpace(item.Name) ? macAddress : item.Name.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var item in request.Proxies ?? [])
        {
            var proxy = request.ReplaceExisting
                ? null
                : await _db.Proxies.FirstOrDefaultAsync(p => p.Name == item.Name && p.Type == item.Type);

            if (proxy == null)
            {
                proxy = new Proxy { CreatedAt = DateTime.UtcNow };
                _db.Proxies.Add(proxy);
            }

            proxy.Name = item.Name;
            proxy.Type = item.Type.ToLowerInvariant();
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
            var rule = request.ReplaceExisting
                ? null
                : await _db.HttpsProxyRules.FirstOrDefaultAsync(r => r.ListenPort == item.ListenPort);

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
}
