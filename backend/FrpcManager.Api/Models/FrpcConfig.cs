namespace FrpcManager.Api.Models;

public class FrpcConfig
{
    public string ServerAddr { get; set; } = "";
    public int ServerPort { get; set; } = 7000;
    public string AuthMethod { get; set; } = "token";
    public string AuthToken { get; set; } = "";
    public string WebServerAddr { get; set; } = "127.0.0.1";
    public int WebServerPort { get; set; } = 7400;
    public List<ProxyConfigEntry> Proxies { get; set; } = new();
}

public class ProxyConfigEntry
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "tcp";
    public string LocalIP { get; set; } = "";
    public int LocalPort { get; set; }
    public int RemotePort { get; set; }
}
