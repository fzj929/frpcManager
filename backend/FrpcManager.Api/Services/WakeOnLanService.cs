using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace FrpcManager.Api.Services;

public class WakeOnLanService
{
    private static readonly Regex HexMacRegex = new("^[0-9a-fA-F]{12}$", RegexOptions.Compiled);

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
