using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class ProxyService
{
    private readonly AppDbContext _db;
    private readonly FrpcApiService _frpcApi;

    public ProxyService(AppDbContext db, FrpcApiService frpcApi)
    {
        _db = db;
        _frpcApi = frpcApi;
    }

    public async Task<List<ProxyResponse>> GetAllProxiesAsync()
    {
        var proxies = await _db.Proxies.OrderBy(p => p.Name).ThenBy(p => p.Type).ToListAsync();
        var statusMap = await BuildStatusMapAsync();
        return proxies.Select(p => ToResponse(p, statusMap)).ToList();
    }

    public async Task<ProxyResponse> CreateProxyAsync(CreateProxyRequest request)
    {
        var proxy = new Proxy
        {
            Name = request.Name,
            Type = request.Type.ToLower(),
            LocalIP = request.LocalIP,
            LocalPort = request.LocalPort,
            RemotePort = request.RemotePort,
            Description = request.Description,
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Proxies.Add(proxy);
        await _db.SaveChangesAsync();
        var statusMap = await BuildStatusMapAsync();
        return ToResponse(proxy, statusMap);
    }

    public async Task<ProxyResponse?> UpdateProxyAsync(int id, UpdateProxyRequest request)
    {
        var proxy = await _db.Proxies.FindAsync(id);
        if (proxy == null) return null;

        var wasEnabled = proxy.IsEnabled;
        proxy.Name = request.Name;
        proxy.Type = request.Type.ToLower();
        proxy.LocalIP = request.LocalIP;
        proxy.LocalPort = request.LocalPort;
        proxy.RemotePort = request.RemotePort;
        proxy.Description = request.Description;
        proxy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (wasEnabled)
            await SyncToFrpcAsync();

        var statusMap = await BuildStatusMapAsync();
        return ToResponse(proxy, statusMap);
    }

    public async Task<bool> DeleteProxyAsync(int id)
    {
        var proxy = await _db.Proxies.FindAsync(id);
        if (proxy == null) return false;

        var wasEnabled = proxy.IsEnabled;
        _db.Proxies.Remove(proxy);
        await _db.SaveChangesAsync();

        if (wasEnabled)
            await SyncToFrpcAsync();

        return true;
    }

    public async Task<(bool Success, string Message)> SetEnabledAsync(int id, bool enabled)
    {
        var proxy = await _db.Proxies.FindAsync(id);
        if (proxy == null) return (false, "通道不存在");

        proxy.IsEnabled = enabled;
        proxy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await SyncToFrpcAsync();
    }

    public async Task<(bool Success, string Message)> SyncToFrpcAsync()
    {
        var currentConfig = await _frpcApi.GetConfigAsync();
        if (currentConfig == null)
            return (false, "无法连接到 frpc API，请检查 frpc 是否正在运行");

        var enabledProxies = await _db.Proxies.Where(p => p.IsEnabled).ToListAsync();
        currentConfig.Proxies = enabledProxies.Select(p => new ProxyConfigEntry
        {
            Name = p.Name,
            Type = p.Type,
            LocalIP = p.LocalIP,
            LocalPort = p.LocalPort,
            RemotePort = p.RemotePort
        }).ToList();

        var putOk = await _frpcApi.PutConfigAsync(currentConfig);
        if (!putOk) return (false, "更新 frpc 配置失败");

        var reloadOk = await _frpcApi.ReloadAsync();
        if (!reloadOk) return (false, "配置已更新，但重新加载失败");

        return (true, "操作成功");
    }

    public async Task SyncFromFrpcAsync()
    {
        var frpcConfig = await _frpcApi.GetConfigAsync();
        if (frpcConfig == null) return;

        var existingProxies = await _db.Proxies.ToListAsync();

        foreach (var p in existingProxies)
            p.IsEnabled = false;

        foreach (var fp in frpcConfig.Proxies)
        {
            var existing = existingProxies.FirstOrDefault(p =>
                string.Equals(p.Name, fp.Name, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.Type, fp.Type, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.IsEnabled = true;
                existing.LocalIP = fp.LocalIP;
                existing.LocalPort = fp.LocalPort;
                existing.RemotePort = fp.RemotePort;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.Proxies.Add(new Proxy
                {
                    Name = fp.Name,
                    Type = fp.Type,
                    LocalIP = fp.LocalIP,
                    LocalPort = fp.LocalPort,
                    RemotePort = fp.RemotePort,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task<Dictionary<string, StatusProxyResponse>> BuildStatusMapAsync()
    {
        var statusData = await _frpcApi.GetStatusAsync();
        var map = new Dictionary<string, StatusProxyResponse>();
        foreach (var (_, proxies) in statusData)
            foreach (var p in proxies)
                map[$"{p.Name}|{p.Type}"] = p;
        return map;
    }

    private static ProxyResponse ToResponse(Proxy p, Dictionary<string, StatusProxyResponse> statusMap)
    {
        var key = $"{p.Name}|{p.Type}";
        statusMap.TryGetValue(key, out var s);
        var status = s?.Status ?? (p.IsEnabled ? "unknown" : "disabled");
        return new ProxyResponse(
            p.Id, p.Name, p.Type, p.LocalIP, p.LocalPort, p.RemotePort,
            p.Description, p.IsEnabled, status,
            s?.RemoteAddr ?? "", s?.Error ?? "",
            p.CreatedAt, p.UpdatedAt
        );
    }
}
