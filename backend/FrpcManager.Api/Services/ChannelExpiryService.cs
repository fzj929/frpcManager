using FrpcManager.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class ChannelExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChannelExpiryService> _logger;

    public ChannelExpiryService(IServiceScopeFactory scopeFactory, ILogger<ChannelExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("通道定时关闭服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndDisableExpiredChannels();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查过期通道时发生错误");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task CheckAndDisableExpiredChannels()
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var proxyService = scope.ServiceProvider.GetRequiredService<ProxyService>();

        var now = DateTime.UtcNow;
        var expired = await db.Proxies
            .Where(p => p.IsEnabled && p.ExpiresAt.HasValue && p.ExpiresAt <= now)
            .ToListAsync();

        if (expired.Count == 0) return;

        _logger.LogInformation("自动关闭 {Count} 个已到期通道：{Names}",
            expired.Count,
            string.Join(", ", expired.Select(p => $"{p.Name}({p.Type})")));

        foreach (var proxy in expired)
        {
            proxy.IsEnabled = false;
            proxy.ExpiresAt = null;
            proxy.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
        await proxyService.SyncToFrpcAsync();
    }
}
