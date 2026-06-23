using FrpcManager.Api.Models;

namespace FrpcManager.Api.DTOs;

public record BackupResponse(
    string Version,
    DateTime ExportedAt,
    List<BackupProxyItem> Proxies,
    List<BackupHttpsProxyItem> HttpsProxies,
    List<BackupWakeMacAddressItem> WakeMacAddresses,
    List<BackupWakeScheduleItem> WakeSchedules,
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

public record BackupWakeScheduleItem(
    string Name,
    string MacAddress,
    string BroadcastAddress,
    int Port,
    string TimeOfDay,
    string ScheduleMode,
    string DaysOfWeek,
    DateTime? SpecificDate,
    bool IsEnabled,
    DateTime? LastRunAt
);

public record RestoreRequest(
    List<BackupProxyItem>? Proxies,
    List<BackupHttpsProxyItem>? HttpsProxies,
    List<BackupWakeMacAddressItem>? WakeMacAddresses,
    List<BackupWakeScheduleItem>? WakeSchedules,
    FrpcConfig? FrpcConfig,
    bool ReplaceExisting = false,
    bool ApplyFrpcConfig = false
);
