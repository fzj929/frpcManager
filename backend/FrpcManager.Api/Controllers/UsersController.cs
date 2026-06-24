using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = UserRoles.Admin)]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogService _auditLogService;

    public UsersController(AppDbContext db, AuditLogService auditLogService)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _db.Users
            .OrderBy(u => u.Username)
            .Select(u => ToResponse(u))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var validation = ValidateUsernamePasswordRole(request.Username, request.Password, request.Role);
        if (validation != null) return validation;

        var username = request.Username.Trim();
        if (await _db.Users.AnyAsync(u => u.Username == username))
            return BadRequest(new { message = "用户名已存在" });

        var user = new User
        {
            Username = username,
            Role = NormalizeRole(request.Role),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "user.create", user.Username, $"role={user.Role}");

        return Ok(ToResponse(user));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        var role = NormalizeRole(request.Role);
        if (!IsValidRole(role))
            return BadRequest(new { message = "角色只能是 admin 或 user" });

        var wouldRemoveAdmin = user.Role == UserRoles.Admin && (role != UserRoles.Admin || request.IsDisabled);
        if (wouldRemoveAdmin && await ActiveAdminCountAsync(excludingUserId: user.Id) == 0)
            return BadRequest(new { message = "不能禁用或降级最后一个管理员" });

        user.Role = role;
        user.IsDisabled = request.IsDisabled;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "user.update", user.Username, $"role={user.Role}; disabled={user.IsDisabled}");

        return Ok(ToResponse(user));
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "密码不能少于 8 位" });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "用户不存在" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _auditLogService.LogAsync(HttpContext, "user.reset-password", user.Username);

        return Ok(new { message = "密码已重置" });
    }

    private BadRequestObjectResult? ValidateUsernamePasswordRole(string username, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(username))
            return BadRequest(new { message = "用户名不能为空" });

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return BadRequest(new { message = "密码不能少于 8 位" });

        if (!IsValidRole(NormalizeRole(role)))
            return BadRequest(new { message = "角色只能是 admin 或 user" });

        return null;
    }

    private async Task<int> ActiveAdminCountAsync(int? excludingUserId = null)
    {
        return await _db.Users.CountAsync(u =>
            u.Role == UserRoles.Admin &&
            !u.IsDisabled &&
            (!excludingUserId.HasValue || u.Id != excludingUserId.Value));
    }

    private static bool IsValidRole(string role) => role is UserRoles.Admin or UserRoles.User;

    private static string NormalizeRole(string role) =>
        string.Equals(role, UserRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? UserRoles.Admin
            : string.Equals(role, UserRoles.User, StringComparison.OrdinalIgnoreCase)
                ? UserRoles.User
                : role.Trim().ToLowerInvariant();

    private static UserResponse ToResponse(User user) => new(
        user.Id,
        user.Username,
        user.Role,
        user.IsDisabled,
        user.CreatedAt,
        user.UpdatedAt);
}
