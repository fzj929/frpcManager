using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using FrpcManager.Api.Data;
using FrpcManager.Api.Models;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class HttpsProxyRuntimeService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HttpsProxyRuntimeService> _logger;
    private readonly ConcurrentDictionary<int, RunningProxy> _running = new();

    public HttpsProxyRuntimeService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<HttpsProxyRuntimeService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartEnabledAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rules = await db.HttpsProxyRules.Where(r => r.IsEnabled).ToListAsync();

        foreach (var rule in rules)
        {
            try
            {
                await StartAsync(rule);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "启动 HTTPS 代理失败：{Name} ({Port})", rule.Name, rule.ListenPort);
            }
        }
    }

    public async Task RestartAsync(HttpsProxyRule rule)
    {
        await StopAsync(rule.Id);
        if (rule.IsEnabled)
            await StartAsync(rule);
    }

    public async Task StopAsync(int ruleId)
    {
        if (!_running.TryRemove(ruleId, out var proxy))
            return;

        await proxy.App.StopAsync(TimeSpan.FromSeconds(5));
        await proxy.App.DisposeAsync();
    }

    private async Task StartAsync(HttpsProxyRule rule)
    {
        if (_running.ContainsKey(rule.Id))
            return;

        var certificate = LoadCertificate(rule);
        var targetBaseUri = NormalizeTargetUri(rule.TargetUrl);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>());

        builder.WebHost.UseKestrel(options =>
        {
            options.ListenAnyIP(rule.ListenPort, listen => listen.UseHttps(certificate));
        });

        builder.Services.AddHttpClient();
        var app = builder.Build();

        app.Run(async context =>
        {
            var clientFactory = context.RequestServices.GetRequiredService<IHttpClientFactory>();
            using var requestMessage = CreateProxyRequest(context, targetBaseUri);
            using var responseMessage = await clientFactory.CreateClient()
                .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

            context.Response.StatusCode = (int)responseMessage.StatusCode;
            CopyResponseHeaders(responseMessage, context);
            await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        });

        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServerAddressesFeature>();
        _running[rule.Id] = new RunningProxy(app);
        _logger.LogInformation("HTTPS 代理已启动：{Name} {Address} -> {Target}", rule.Name, string.Join(",", addresses.Addresses), targetBaseUri);
    }

    private X509Certificate2 LoadCertificate(HttpsProxyRule rule)
    {
        if (rule.CertificateMode == "pfx" && !string.IsNullOrWhiteSpace(rule.CertificatePath))
            return new X509Certificate2(rule.CertificatePath, rule.CertificatePassword);

        if (rule.CertificateMode == "pem" &&
            !string.IsNullOrWhiteSpace(rule.CertificatePath) &&
            !string.IsNullOrWhiteSpace(rule.CertificateKeyPath))
        {
            var certificate = string.IsNullOrWhiteSpace(rule.CertificatePassword)
                ? X509Certificate2.CreateFromPemFile(rule.CertificatePath, rule.CertificateKeyPath)
                : X509Certificate2.CreateFromEncryptedPemFile(
                    rule.CertificatePath,
                    rule.CertificatePassword,
                    rule.CertificateKeyPath);
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
        }

        var path = _configuration["Kestrel:Endpoints:Https:Certificate:Path"] ?? "frpcmanager.pfx";
        var password = _configuration["Kestrel:Endpoints:Https:Certificate:Password"] ?? "";
        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppContext.BaseDirectory, path);

        return new X509Certificate2(path, password);
    }

    private static Uri NormalizeTargetUri(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
            throw new InvalidOperationException("目标地址必须是有效的 HTTP URL");

        return uri;
    }

    private static HttpRequestMessage CreateProxyRequest(HttpContext context, Uri targetBaseUri)
    {
        var targetUri = BuildTargetUri(targetBaseUri, context.Request.Path, context.Request.QueryString);
        var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

        if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            requestMessage.Content = new StreamContent(context.Request.Body);

        foreach (var header in context.Request.Headers)
        {
            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.ToString());
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        requestMessage.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress?.ToString());
        return requestMessage;
    }

    private static Uri BuildTargetUri(Uri targetBaseUri, PathString requestPath, QueryString queryString)
    {
        var basePath = targetBaseUri.AbsolutePath.TrimEnd('/');
        var path = requestPath.Value ?? "";
        var builder = new UriBuilder(targetBaseUri)
        {
            Path = $"{basePath}/{path.TrimStart('/')}".TrimEnd('/'),
            Query = queryString.HasValue ? queryString.Value!.TrimStart('?') : ""
        };

        return builder.Uri;
    }

    private static void CopyResponseHeaders(HttpResponseMessage responseMessage, HttpContext context)
    {
        foreach (var header in responseMessage.Headers)
            context.Response.Headers[header.Key] = header.Value.ToArray();

        foreach (var header in responseMessage.Content.Headers)
            context.Response.Headers[header.Key] = header.Value.ToArray();

        context.Response.Headers.Remove("transfer-encoding");
    }

    private sealed record RunningProxy(WebApplication App);
}
