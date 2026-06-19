using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FrpcManager.Api.Services;

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly JwtKeyProvider _jwtKeyProvider;
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutMinutes;

    public AuthService(AppDbContext db, IConfiguration config, JwtKeyProvider jwtKeyProvider)
    {
        _db = db;
        _config = config;
        _jwtKeyProvider = jwtKeyProvider;
        _maxFailedAttempts = config.GetValue("LoginSecurity:MaxFailedAttempts", 5);
        _lockoutMinutes = config.GetValue("LoginSecurity:LockoutMinutes", 10);
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var username = request.Username.Trim();
        var now = DateTime.UtcNow;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return LoginResult.Failed();

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > now)
            return LoginResult.Locked(user.FailedLoginCount, user.LockedUntil.Value);

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            DateTime? lockedUntil = null;
            if (user.FailedLoginCount >= _maxFailedAttempts)
            {
                lockedUntil = now.AddMinutes(_lockoutMinutes);
                user.LockedUntil = lockedUntil;
            }

            await _db.SaveChangesAsync();
            return lockedUntil.HasValue
                ? LoginResult.Locked(user.FailedLoginCount, lockedUntil.Value)
                : LoginResult.Failed(user.FailedLoginCount);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        await _db.SaveChangesAsync();

        var token = GenerateToken(user);
        var expiry = now.AddHours(
            double.TryParse(_config["Jwt:ExpiryInHours"], out var h) ? h : 24);
        return LoginResult.Success(new LoginResponse(token, user.Username, expiry));
    }

    public async Task<bool> ChangePasswordAsync(string username, ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return false;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    private string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKeyProvider.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(
            double.TryParse(_config["Jwt:ExpiryInHours"], out var h) ? h : 24);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public enum LoginResultStatus
{
    Success,
    Failed,
    Locked
}

public record LoginResult(
    LoginResultStatus Status,
    LoginResponse? Response = null,
    int FailedAttempts = 0,
    DateTime? LockedUntil = null)
{
    public static LoginResult Success(LoginResponse response) => new(LoginResultStatus.Success, response);
    public static LoginResult Failed(int failedAttempts = 0) => new(LoginResultStatus.Failed, FailedAttempts: failedAttempts);
    public static LoginResult Locked(int failedAttempts, DateTime lockedUntil) =>
        new(LoginResultStatus.Locked, FailedAttempts: failedAttempts, LockedUntil: lockedUntil);
}
