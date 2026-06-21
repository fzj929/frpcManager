namespace FrpcManager.Api.Services;

public class HttpsProxyStartupService : BackgroundService
{
    private readonly HttpsProxyRuntimeService _proxyRuntime;

    public HttpsProxyStartupService(HttpsProxyRuntimeService proxyRuntime)
    {
        _proxyRuntime = proxyRuntime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);
        await _proxyRuntime.StartEnabledAsync();
    }
}
