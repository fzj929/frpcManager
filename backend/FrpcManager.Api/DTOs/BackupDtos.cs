using FrpcManager.Api.Models;

namespace FrpcManager.Api.DTOs;

public record BackupResponse(
    string Version,
    DateTime ExportedAt,
    List<BackupProxyItem> Proxies,
    List<BackupHttpsProxyItem> HttpsProxies,
    List<BackupWakeMacAddressItem> WakeMacAddresses,
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

public record BackupHttpsProxyItem(
    string Name,
    int ListenPort,
    string TargetUrl,
    string CertificateMode,
    string Description,
    bool IsEnabled
);

public record BackupWakeMacAddressItem(
    string MacAddress,
    string Name
);

public record RestoreRequest(
    List<BackupProxyItem>? Proxies,
    List<BackupHttpsProxyItem>? HttpsProxies,
    List<BackupWakeMacAddressItem>? WakeMacAddresses,
    FrpcConfig? FrpcConfig,
    bool ReplaceExisting = false,
    bool ApplyFrpcConfig = false
);
