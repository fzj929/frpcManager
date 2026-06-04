using System.Text.Json;
using System.Text.Json.Serialization;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;

namespace FrpcManager.Api.Services;

public class FrpcApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TomlService _tomlService;
    private readonly ILogger<FrpcApiService> _logger;

    public FrpcApiService(
        IHttpClientFactory httpClientFactory,
        TomlService tomlService,
        ILogger<FrpcApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tomlService = tomlService;
        _logger = logger;
    }

    private HttpClient CreateClient() => _httpClientFactory.CreateClient("FrpcApi");

    public async Task<FrpcConfig?> GetConfigAsync()
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync("/api/config");
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync();
            return _tomlService.Parse(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot reach frpc API for config");
            return null;
        }
    }

    public async Task<bool> PutConfigAsync(FrpcConfig config)
    {
        try
        {
            var client = CreateClient();
            var toml = _tomlService.Serialize(config);
            var content = new StringContent(toml, System.Text.Encoding.UTF8, "text/plain");
            var response = await client.PutAsync("/api/config", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot reach frpc API to update config");
            return false;
        }
    }

    public async Task<bool> ReloadAsync()
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync("/api/reload");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot reach frpc API to reload");
            return false;
        }
    }

    public async Task<Dictionary<string, List<StatusProxyResponse>>> GetStatusAsync()
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync("/api/status");
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, List<StatusProxyResponse>>();

            var content = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(content);
            var result = new Dictionary<string, List<StatusProxyResponse>>();

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var list = new List<StatusProxyResponse>();
                foreach (var item in prop.Value.EnumerateArray())
                {
                    list.Add(new StatusProxyResponse(
                        item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        item.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                        item.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
                        item.TryGetProperty("local_addr", out var la) ? la.GetString() ?? "" : "",
                        item.TryGetProperty("remote_addr", out var ra) ? ra.GetString() ?? "" : "",
                        item.TryGetProperty("err", out var e) ? e.GetString() ?? "" : ""
                    ));
                }
                result[prop.Name] = list;
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot reach frpc API for status");
            return new Dictionary<string, List<StatusProxyResponse>>();
        }
    }
}
