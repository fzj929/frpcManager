namespace FrpcManager.Api.Models;

public class WakeSchedule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string MacAddress { get; set; } = "";
    public string BroadcastAddress { get; set; } = "";
    public int Port { get; set; } = 9;
    public string TimeOfDay { get; set; } = "08:00";
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
