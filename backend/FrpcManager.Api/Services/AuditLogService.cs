using System.Security.Claims;
using FrpcManager.Api.Data;
using FrpcManager.Api.DTOs;
using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Services;

public class AuditLogService
{
    private readonly AppDbContext _db;

    public AuditLogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(HttpContext httpContext, string action, string target, string details = "", bool success = true)
    {
        var username = httpContext.User.FindFirst(ClaimTypes.Name)?.Value;
        if (string.IsNullOrWhiteSpace(username))
            username = "anonymous";

        _db.AuditLogs.Add(new AuditLog
        {
            Username = username,
            Action = action,
            Target = target,
            Details = details,
            Success = success,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLogResponse>> GetRecentAsync(int limit = 200)
    {
        limit = Math.Clamp(limit, 1, 1000);
        return await _db.AuditLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new AuditLogResponse(
                l.Id,
                l.Username,
                l.Action,
                l.Target,
                l.Details,
                l.IpAddress,
                l.Success,
                l.CreatedAt
            ))
            .ToListAsync();
    }
}
