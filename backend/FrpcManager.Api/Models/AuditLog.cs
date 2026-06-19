namespace FrpcManager.Api.Models;

public class AuditLog
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string Details { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool Success { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
