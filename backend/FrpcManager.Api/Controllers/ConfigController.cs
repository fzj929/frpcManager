using FrpcManager.Api.DTOs;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfigController : ControllerBase
{
    private readonly FrpcApiService _frpcApi;

    public ConfigController(FrpcApiService frpcApi) => _frpcApi = frpcApi;

    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        var config = await _frpcApi.GetConfigAsync();
        if (config == null)
            return StatusCode(503, new { message = "无法连接到 frpc API，请检查 frpc 是否正在运行" });

        return Ok(new FrpcConfigResponse(
            config.ServerAddr, config.ServerPort,
            config.AuthMethod, config.AuthToken,
            config.WebServerAddr, config.WebServerPort
        ));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] FrpcConfigRequest request)
    {
        var currentConfig = await _frpcApi.GetConfigAsync();
        if (currentConfig == null)
            return StatusCode(503, new { message = "无法连接到 frpc API" });

        currentConfig.ServerAddr = request.ServerAddr;
        currentConfig.ServerPort = request.ServerPort;
        currentConfig.AuthMethod = request.AuthMethod;
        currentConfig.AuthToken = request.AuthToken;
        currentConfig.WebServerAddr = request.WebServerAddr;
        currentConfig.WebServerPort = request.WebServerPort;

        var putOk = await _frpcApi.PutConfigAsync(currentConfig);
        if (!putOk) return StatusCode(500, new { message = "更新 frpc 配置失败" });

        var reloadOk = await _frpcApi.ReloadAsync();
        if (!reloadOk) return StatusCode(500, new { message = "配置已更新，但重新加载失败" });

        return Ok(new { message = "配置更新并重新加载成功" });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var status = await _frpcApi.GetStatusAsync();
        return Ok(status);
    }

    [HttpPost("reload")]
    public async Task<IActionResult> Reload()
    {
        var ok = await _frpcApi.ReloadAsync();
        if (!ok) return StatusCode(500, new { message = "重新加载失败" });
        return Ok(new { message = "重新加载成功" });
    }
}
