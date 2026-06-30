using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace FrpcManager.Api.Services;

public class WakeOnLanService
{
    private static readonly Regex HexMacRegex = new("^[0-9a-fA-F]{12}$", RegexOptions.Compiled);
    private static readonly Regex HostRegex = new("^[a-zA-Z0-9.-]+$", RegexOptions.Compiled);

    public async Task SendMagicPacketAsync(string macAddress, string broadcastAddress, int port)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentException("端口必须在 1-65535 之间");

        var macBytes = ParseMacAddress(macAddress);

        if (!IPAddress.TryParse(broadcastAddress, out var ipAddress))
            throw new ArgumentException("广播地址格式不正确");

        var packet = BuildMagicPacket(macBytes);

        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;
        await udpClient.SendAsync(packet, packet.Length, new IPEndPoint(ipAddress, port));
    }

    public static string NormalizeMacAddress(string macAddress)
    {
        var normalized = macAddress
            .Replace("-", string.Empty)
            .Replace(":", string.Empty)
            .Replace(".", string.Empty)
            .Trim();

        if (!HexMacRegex.IsMatch(normalized))
            throw new ArgumentException("MAC 地址格式不正确");

        return string.Join(':', Enumerable.Range(0, 6)
            .Select(i => normalized.Substring(i * 2, 2).ToUpperInvariant()));
    }

    public async Task<PingReply> PingAsync(string host, int timeoutMs)
    {
        var normalizedHost = NormalizePingHost(host);
        var timeout = Math.Clamp(timeoutMs <= 0 ? 3000 : timeoutMs, 1000, 10000);
        using var ping = new Ping();
        return await ping.SendPingAsync(normalizedHost, timeout);
    }

    public static string NormalizePingHost(string host)
    {
        var normalized = host?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("请输入要测试的 IP 或域名");

        if (normalized.Length > 253)
            throw new ArgumentException("主机地址不能超过 253 个字符");

        if (!IPAddress.TryParse(normalized, out _) && !HostRegex.IsMatch(normalized))
            throw new ArgumentException("主机地址只能填写 IP 或域名");

        return normalized;
    }

    private static byte[] ParseMacAddress(string macAddress)
    {
        var normalized = NormalizeMacAddress(macAddress).Replace(":", string.Empty);

        var bytes = new byte[6];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(normalized.Substring(i * 2, 2), 16);
        }

        return bytes;
    }

    private static byte[] BuildMagicPacket(byte[] macBytes)
    {
        var packet = new byte[6 + 16 * macBytes.Length];
        Array.Fill<byte>(packet, 0xFF, 0, 6);

        for (var i = 6; i < packet.Length; i += macBytes.Length)
        {
            Buffer.BlockCopy(macBytes, 0, packet, i, macBytes.Length);
        }

        return packet;
    }
}
