namespace FrpcManager.Api.Models;

public class WakeLog
{
    public int Id { get; set; }
    public string MacAddress { get; set; } = "";
    public string BroadcastAddress { get; set; } = "";
    public int Port { get; set; } = 9;
    public string Source { get; set; } = "manual";
    public string Username { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public bool Success { get; set; } = true;
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
