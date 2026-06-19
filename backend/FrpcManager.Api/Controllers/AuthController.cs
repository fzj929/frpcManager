using System.Security.Claims;
using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly AppDbContext _db;
    private readonly AuditLogService _auditLogService;

    public AuthController(AuthService authService, AppDbContext db, AuditLogService auditLogService)
    {
        _authService = authService;
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet("setup-status")]
    [AllowAnonymous]
    public async Task<IActionResult> SetupStatus()
    {
        return Ok(new SetupStatusResponse(!await _db.Users.AnyAsync()));
    }

    [HttpPost("setup")]
    [AllowAnonymous]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        if (await _db.Users.AnyAsync())
            return BadRequest(new { message = "系统已完成初始化" });

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "用户名和密码不能为空" });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "密码不能少于 8 位" });

        var username = request.Username.Trim();
        _db.Users.Add(new User
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "setup.create-admin", "user", username);

        return Ok(new { message = "初始化完成" });
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "用户名和密码不能为空" });

        var result = await _authService.LoginAsync(request);
        if (result == null)
        {
            await _auditLogService.LogAsync(HttpContext, "auth.login", request.Username, "login failed", false);
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        await _auditLogService.LogAsync(HttpContext, "auth.login", request.Username, "login succeeded");
        return Ok(result);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var success = await _authService.ChangePasswordAsync(username, request);
        if (!success)
        {
            await _auditLogService.LogAsync(HttpContext, "auth.change-password", username, "change password failed", false);
            return BadRequest(new { message = "当前密码错误或新密码不符合要求" });
        }

        await _auditLogService.LogAsync(HttpContext, "auth.change-password", username);
        return Ok(new { message = "密码修改成功" });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        return Ok(new { username });
    }
}
