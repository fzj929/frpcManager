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

    public async Task<List<ProxyResponse>> GetAllProxiesAsync(int? currentUserId, bool isAdmin)
    {
        var proxies = await _db.Proxies
            .Include(p => p.CreatedByUser)
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Type)
            .ToListAsync();
        var statusMap = await BuildStatusMapAsync();
        return proxies.Select(p => ToResponse(p, statusMap, currentUserId, isAdmin)).ToList();
    }

    public async Task<ProxyResponse> CreateProxyAsync(CreateProxyRequest request, int? createdByUserId = null)
    {
        var proxy = new Proxy
        {
            Name = request.Name,
            Type = request.Type.ToLowerInvariant(),
            LocalIP = request.LocalIP,
            LocalPort = request.LocalPort,
            RemotePort = request.RemotePort,
            Description = request.Description,
            IsEnabled = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Proxies.Add(proxy);
        await _db.SaveChangesAsync();
        await _db.Entry(proxy).Reference(p => p.CreatedByUser).LoadAsync();

        var statusMap = await BuildStatusMapAsync();
        return ToResponse(proxy, statusMap, createdByUserId, true);
    }

    public async Task<(bool Success, string Message, ProxyResponse? Proxy)> UpdateProxyAsync(
        int id,
        UpdateProxyRequest request,
        int? currentUserId,
        bool isAdmin)
    {
        var proxy = await _db.Proxies
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proxy == null) return (false, "通道不存在", null);
        if (!CanManage(proxy, currentUserId, isAdmin)) return (false, "没有权限修改这个通道", null);

        if (proxy.IsEnabled)
        {
            var conflict = await FindRemotePortConflictAsync(request.Type, request.RemotePort, id);
            if (conflict != null)
                return (false, $"远程端口 {request.RemotePort} 已被已启用通道“{conflict.Name}”使用", null);
        }

        var wasEnabled = proxy.IsEnabled;
        proxy.Name = request.Name;
        proxy.Type = request.Type.ToLowerInvariant();
        proxy.LocalIP = request.LocalIP;
        proxy.LocalPort = request.LocalPort;
        proxy.RemotePort = request.RemotePort;
        proxy.Description = request.Description;
        proxy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (wasEnabled)
        {
            var syncResult = await SyncToFrpcAsync();
            if (!syncResult.Success)
                return (false, syncResult.Message, null);
        }

        var statusMap = await BuildStatusMapAsync();
        return (true, "操作成功", ToResponse(proxy, statusMap, currentUserId, isAdmin));
    }

    public async Task<(bool Success, string Message)> DeleteProxyAsync(int id, int? currentUserId = null, bool isAdmin = true)
    {
        var proxy = await _db.Proxies.FindAsync(id);
        if (proxy == null) return (false, "通道不存在");
        if (!CanManage(proxy, currentUserId, isAdmin)) return (false, "没有权限删除这个通道");

        var wasEnabled = proxy.IsEnabled;
        _db.Proxies.Remove(proxy);
        await _db.SaveChangesAsync();

        if (wasEnabled)
            return await SyncToFrpcAsync();

        return (true, "删除成功");
    }

    public async Task<(bool Success, string Message)> SetEnabledAsync(
        int id,
        bool enabled,
        int? durationMinutes = null,
        int? currentUserId = null,
        bool isAdmin = true)
    {
        var proxy = await _db.Proxies.FindAsync(id);
        if (proxy == null) return (false, "通道不存在");
        if (!CanManage(proxy, currentUserId, isAdmin)) return (false, "没有权限操作这个通道");

        if (enabled)
        {
            var conflict = await FindRemotePortConflictAsync(proxy.Type, proxy.RemotePort, proxy.Id);
            if (conflict != null)
                return (false, $"远程端口 {proxy.RemotePort} 已被已启用通道“{conflict.Name}”使用，不能同时启用");
        }

        var oldEnabled = proxy.IsEnabled;
        var oldExpiresAt = proxy.ExpiresAt;
        proxy.IsEnabled = enabled;
        proxy.UpdatedAt = DateTime.UtcNow;
        proxy.ExpiresAt = enabled && durationMinutes.HasValue
            ? DateTime.UtcNow.AddMinutes(durationMinutes.Value)
            : null;

        await _db.SaveChangesAsync();
        var syncResult = await SyncToFrpcAsync();
        if (syncResult.Success) return syncResult;

        proxy.IsEnabled = oldEnabled;
        proxy.ExpiresAt = oldExpiresAt;
        proxy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return syncResult;
    }

    public async Task<(bool Success, string Message, ProxyResponse? Proxy)> AssignOwnerAsync(
        int id,
        int? ownerUserId,
        int? currentUserId,
        bool isAdmin)
    {
        if (!isAdmin) return (false, "只有管理员可以分配通道归属", null);

        var proxy = await _db.Proxies
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proxy == null) return (false, "通道不存在", null);

        User? owner = null;
        if (ownerUserId.HasValue)
        {
            owner = await _db.Users.FindAsync(ownerUserId.Value);
            if (owner == null) return (false, "指定用户不存在", null);
        }

        proxy.CreatedByUserId = ownerUserId;
        proxy.CreatedByUser = owner;
        proxy.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var statusMap = await BuildStatusMapAsync();
        return (true, "通道归属已更新", ToResponse(proxy, statusMap, currentUserId, isAdmin));
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
                    CreatedByUserId = null,
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

    private async Task<Proxy?> FindRemotePortConflictAsync(string type, int remotePort, int currentId)
    {
        var normalizedType = type.ToLowerInvariant();
        if (normalizedType is not ("tcp" or "udp"))
            return null;

        return await _db.Proxies
            .Where(p =>
                p.Id != currentId &&
                p.IsEnabled &&
                p.RemotePort == remotePort &&
                p.Type.ToLower() == normalizedType)
            .OrderBy(p => p.Name)
            .FirstOrDefaultAsync();
    }

    private static bool CanManage(Proxy proxy, int? currentUserId, bool isAdmin) =>
        isAdmin || (currentUserId.HasValue && proxy.CreatedByUserId == currentUserId.Value);

    private static ProxyResponse ToResponse(
        Proxy p,
        Dictionary<string, StatusProxyResponse> statusMap,
        int? currentUserId,
        bool isAdmin)
    {
        var key = $"{p.Name}|{p.Type}";
        statusMap.TryGetValue(key, out var s);
        var status = s?.Status ?? (p.IsEnabled ? "unknown" : "disabled");
        return new ProxyResponse(
            p.Id, p.Name, p.Type, p.LocalIP, p.LocalPort, p.RemotePort,
            p.Description, p.IsEnabled, status,
            s?.RemoteAddr ?? "", s?.Error ?? "",
            p.CreatedByUserId, p.CreatedByUser?.Username ?? "",
            isAdmin || (currentUserId.HasValue && p.CreatedByUserId == currentUserId.Value),
            p.CreatedAt, p.UpdatedAt, p.ExpiresAt
        );
    }
}
