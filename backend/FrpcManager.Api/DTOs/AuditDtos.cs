namespace FrpcManager.Api.DTOs;

public record AuditLogResponse(
    int Id,
    string Username,
    string Action,
    string Target,
    string Details,
    string IpAddress,
    bool Success,
    DateTime CreatedAt
);
