using FrpcManager.Api.Models;

namespace FrpcManager.Api.DTOs;

public record BackupResponse(
    string Version,
    DateTime ExportedAt,
    List<BackupProxyItem> Proxies,
    FrpcConfig? FrpcConfig
);

public record BackupProxyItem(
    string Name,
    string Type,
    string LocalIP,
    int LocalPort,
    int RemotePort,
    string Description,
    bool IsEnabled,
    DateTime? ExpiresAt
);

public record RestoreRequest(
    List<BackupProxyItem> Proxies,
    FrpcConfig? FrpcConfig,
    bool ReplaceExisting = true,
    bool ApplyFrpcConfig = true
);
