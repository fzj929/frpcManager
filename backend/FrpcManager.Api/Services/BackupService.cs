using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class BackupService
{
    private readonly AppDbContext _db;
    private readonly FrpcApiService _frpcApi;

    public BackupService(AppDbContext db, FrpcApiService frpcApi)
    {
        _db = db;
        _frpcApi = frpcApi;
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

        var frpcConfig = await _frpcApi.GetConfigAsync();
        return new BackupResponse("1", DateTime.UtcNow, proxies, frpcConfig);
    }

    public async Task RestoreAsync(RestoreRequest request)
    {
        if (request.ReplaceExisting)
        {
            _db.Proxies.RemoveRange(_db.Proxies);
        }

        foreach (var item in request.Proxies)
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

        await _db.SaveChangesAsync();

        if (request.ApplyFrpcConfig && request.FrpcConfig != null)
        {
            await _frpcApi.PutConfigAsync(request.FrpcConfig);
            await _frpcApi.ReloadAsync();
        }
    }
}
