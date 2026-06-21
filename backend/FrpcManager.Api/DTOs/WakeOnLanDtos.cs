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

public record WakeLogResponse(
    int Id,
    string MacAddress,
    string BroadcastAddress,
    int Port,
    string Source,
    string Username,
    string IpAddress,
    bool Success,
    string Message,
    DateTime CreatedAt
);

public record WakeScheduleRequest(
    string Name,
    string MacAddress,
    string? BroadcastAddress,
    int Port,
    string TimeOfDay,
    bool IsEnabled
);

public record WakeScheduleResponse(
    int Id,
    string Name,
    string MacAddress,
    string BroadcastAddress,
    int Port,
    string TimeOfDay,
    bool IsEnabled,
    DateTime? LastRunAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
