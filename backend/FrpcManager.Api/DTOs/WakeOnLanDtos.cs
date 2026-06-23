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
    string MacName,
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
    string? ScheduleMode,
    string? DaysOfWeek,
    DateTime? SpecificDate,
    bool IsEnabled
);

public record WakeScheduleResponse(
    int Id,
    string Name,
    string MacAddress,
    string MacName,
    string BroadcastAddress,
    int Port,
    string TimeOfDay,
    string ScheduleMode,
    string DaysOfWeek,
    DateTime? SpecificDate,
    bool IsEnabled,
    DateTime? LastRunAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record WakeMacAddressRequest(
    string MacAddress,
    string? Name
);

public record WakeMacAddressResponse(
    int Id,
    string MacAddress,
    string Name,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
