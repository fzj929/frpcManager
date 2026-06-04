namespace FrpcManager.Api.Models;

public class Proxy
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "tcp";
    public string LocalIP { get; set; } = "";
    public int LocalPort { get; set; }
    public int RemotePort { get; set; }
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
