using System.Text;
using FrpcManager.Api.Models;

namespace FrpcManager.Api.Services;

public class TomlService
{
    public FrpcConfig Parse(string content)
    {
        var config = new FrpcConfig();
        var lines = content.Split('\n').Select(l => l.Trim()).ToArray();
        ProxyConfigEntry? currentProxy = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            if (line == "[[proxies]]")
            {
                if (currentProxy != null)
                    config.Proxies.Add(currentProxy);
                currentProxy = new ProxyConfigEntry();
                continue;
            }

            var eqIdx = line.IndexOf('=');
            if (eqIdx < 0) continue;

            var key = line[..eqIdx].Trim();
            var rawValue = line[(eqIdx + 1)..].Trim();
            var strValue = rawValue.Trim('"');

            if (currentProxy != null)
            {
                switch (key)
                {
                    case "name": currentProxy.Name = strValue; break;
                    case "type": currentProxy.Type = strValue; break;
                    case "localIP": currentProxy.LocalIP = strValue; break;
                    case "localPort":
                        if (int.TryParse(strValue, out var lp)) currentProxy.LocalPort = lp;
                        break;
                    case "remotePort":
                        if (int.TryParse(strValue, out var rp)) currentProxy.RemotePort = rp;
                        break;
                }
            }
            else
            {
                switch (key)
                {
                    case "serverAddr": config.ServerAddr = strValue; break;
                    case "serverPort":
                        if (int.TryParse(strValue, out var sp)) config.ServerPort = sp;
                        break;
                    case "auth.method": config.AuthMethod = strValue; break;
                    case "auth.token": config.AuthToken = strValue; break;
                    case "webServer.addr": config.WebServerAddr = strValue; break;
                    case "webServer.port":
                        if (int.TryParse(strValue, out var wp)) config.WebServerPort = wp;
                        break;
                }
            }
        }

        if (currentProxy != null)
            config.Proxies.Add(currentProxy);

        return config;
    }

    public string Serialize(FrpcConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"serverAddr = \"{config.ServerAddr}\"");
        sb.AppendLine($"serverPort = {config.ServerPort}");
        sb.AppendLine();
        sb.AppendLine($"auth.method = \"{config.AuthMethod}\"");
        sb.AppendLine($"auth.token = \"{config.AuthToken}\"");
        sb.AppendLine();
        sb.AppendLine($"webServer.addr = \"{config.WebServerAddr}\"");
        sb.AppendLine($"webServer.port = {config.WebServerPort}");

        foreach (var proxy in config.Proxies)
        {
            sb.AppendLine();
            sb.AppendLine("[[proxies]]");
            sb.AppendLine($"name = \"{proxy.Name}\"");
            sb.AppendLine($"type = \"{proxy.Type}\"");
            sb.AppendLine($"localIP = \"{proxy.LocalIP}\"");
            sb.AppendLine($"localPort = {proxy.LocalPort}");
            sb.AppendLine($"remotePort = {proxy.RemotePort}");
        }

        return sb.ToString();
    }
}
