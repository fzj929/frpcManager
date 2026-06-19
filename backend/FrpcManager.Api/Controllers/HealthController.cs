using FrpcManager.Api.Data;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FrpcApiService _frpcApi;

    public HealthController(AppDbContext db, FrpcApiService frpcApi)
    {
        _db = db;
        _frpcApi = frpcApi;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var databaseOk = await _db.Database.CanConnectAsync();
        var frpcOk = await _frpcApi.GetConfigAsync() != null;
        var ok = databaseOk;

        return StatusCode(ok ? 200 : 503, new
        {
            status = ok ? "healthy" : "unhealthy",
            database = databaseOk ? "ok" : "failed",
            frpc = frpcOk ? "ok" : "unreachable",
            checkedAt = DateTime.UtcNow
        });
    }
}
