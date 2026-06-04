namespace FrpcManager.Api.DTOs;

public record CreateProxyRequest(
    string Name,
    string Type,
    string LocalIP,
    int LocalPort,
    int RemotePort,
    string Description
);

public record UpdateProxyRequest(
    string Name,
    string Type,
    string LocalIP,
    int LocalPort,
    int RemotePort,
    string Description
);

public record EnableRequest(int? DurationMinutes);

public record ProxyResponse(
    int Id,
    string Name,
    string Type,
    string LocalIP,
    int LocalPort,
    int RemotePort,
    string Description,
    bool IsEnabled,
    string Status,
    string RemoteAddr,
    string ErrorMsg,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ExpiresAt
);
