namespace FrpcManager.Api.DTOs;

public record FrpcConfigRequest(
    string ServerAddr,
    int ServerPort,
    string AuthMethod,
    string AuthToken,
    string WebServerAddr,
    int WebServerPort
);

public record FrpcConfigResponse(
    string ServerAddr,
    int ServerPort,
    string AuthMethod,
    string AuthToken,
    string WebServerAddr,
    int WebServerPort
);

public record StatusProxyResponse(
    string Name,
    string Type,
    string Status,
    string LocalAddr,
    string RemoteAddr,
    string Error
);
