namespace FrpcManager.Api.Models;

public class WakeMacAddress
{
    public int Id { get; set; }
    public string MacAddress { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
