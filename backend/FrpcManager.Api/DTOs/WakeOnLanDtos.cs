namespace FrpcManager.Api.DTOs;

public record WakeOnLanRequest(
    string MacAddress,
    string? BroadcastAddress,
    int Port = 9
);

public record WakeOnLanResponse(
    string MacAddress,
    string BroadcastAddress,
    int Port,
    string Message
);
