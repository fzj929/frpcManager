namespace FrpcManager.Api.DTOs;

public record SetupStatusResponse(bool Required);

public record SetupRequest(string Username, string Password);
