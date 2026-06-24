namespace FrpcManager.Api.DTOs;

public record UserResponse(
    int Id,
    string Username,
    string Role,
    bool IsDisabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record CreateUserRequest(string Username, string Password, string Role);

public record UpdateUserRequest(string Role, bool IsDisabled);

public record ResetPasswordRequest(string NewPassword);
