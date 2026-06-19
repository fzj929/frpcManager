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
    private readonly LoginAttemptLimiter _loginAttemptLimiter;

    public AuthController(
        AuthService authService,
        AppDbContext db,
        AuditLogService auditLogService,
        LoginAttemptLimiter loginAttemptLimiter)
    {
        _authService = authService;
        _db = db;
        _auditLogService = auditLogService;
        _loginAttemptLimiter = loginAttemptLimiter;
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

        var username = request.Username.Trim();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (_loginAttemptLimiter.IsLimited(ipAddress, username, DateTime.UtcNow))
        {
            await _auditLogService.LogAsync(
                HttpContext,
                "auth.login.rate-limited",
                username,
                $"too many login attempts; ip={ipAddress}",
                false);
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "登录尝试过于频繁，请稍后再试" });
        }

        var result = await _authService.LoginAsync(request);
        if (result.Status == LoginResultStatus.Locked)
        {
            await _auditLogService.LogAsync(
                HttpContext,
                "auth.login.locked",
                username,
                $"failedAttempts={result.FailedAttempts}; lockedUntil={result.LockedUntil:O}; ip={ipAddress}",
                false);
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "账号已临时锁定，请稍后再试",
                failedAttempts = result.FailedAttempts,
                lockedUntil = result.LockedUntil
            });
        }

        if (result.Status == LoginResultStatus.Failed)
        {
            await _auditLogService.LogAsync(
                HttpContext,
                "auth.login",
                username,
                $"login failed; failedAttempts={result.FailedAttempts}; ip={ipAddress}",
                false);
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        await _auditLogService.LogAsync(HttpContext, "auth.login", username, $"login succeeded; ip={ipAddress}");
        return Ok(result.Response);
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
