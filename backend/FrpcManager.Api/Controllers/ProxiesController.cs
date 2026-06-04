using FrpcManager.Api.DTOs;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProxiesController : ControllerBase
{
    private readonly ProxyService _proxyService;

    public ProxiesController(ProxyService proxyService) => _proxyService = proxyService;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _proxyService.GetAllProxiesAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProxyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "通道名称不能为空" });
        var result = await _proxyService.CreateProxyAsync(request);
        return Ok(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProxyRequest request)
    {
        var result = await _proxyService.UpdateProxyAsync(id, request);
        if (result == null) return NotFound(new { message = "通道不存在" });
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _proxyService.DeleteProxyAsync(id);
        if (!success) return NotFound(new { message = "通道不存在" });
        return Ok(new { message = "删除成功" });
    }

    [HttpPut("{id:int}/enable")]
    public async Task<IActionResult> Enable(int id)
    {
        var (success, message) = await _proxyService.SetEnabledAsync(id, true);
        if (!success) return BadRequest(new { message });
        return Ok(new { message = "通道已启用" });
    }

    [HttpPut("{id:int}/disable")]
    public async Task<IActionResult> Disable(int id)
    {
        var (success, message) = await _proxyService.SetEnabledAsync(id, false);
        if (!success) return BadRequest(new { message });
        return Ok(new { message = "通道已停用" });
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncFromFrpc()
    {
        await _proxyService.SyncFromFrpcAsync();
        return Ok(new { message = "已从 frpc 同步配置" });
    }
}
