namespace FrpcManager.Api.DTOs;

public record HttpsProxyRuleResponse(
    int Id,
    string Name,
    int ListenPort,
    string TargetUrl,
    string CertificateMode,
    bool HasCustomCertificate,
    bool HasPrivateKey,
    string Description,
    bool IsEnabled,
    int? CreatedByUserId,
    string CreatedByUsername,
    bool CanManage,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record HttpsProxyRuleRequest(
    string Name,
    int ListenPort,
    string TargetUrl,
    string CertificateMode,
    string? CertificatePassword,
    string? Description,
    bool IsEnabled,
    bool CreateFrpChannel,
    string? FrpChannelName
);

public record AssignHttpsProxyOwnerRequest(int? UserId);
