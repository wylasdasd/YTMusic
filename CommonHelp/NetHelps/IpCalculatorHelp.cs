using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace CommonHelp;

/// <summary>
/// 功能职责：IPv4 子网计算结果（地址、掩码、网络/广播、主机范围与主机数）。
/// </summary>
public sealed record IpCalculatorResult(
    string Address,
    string IntegerValue,
    string HexValue,
    string SubnetMask,
    string WildcardMask,
    string NetworkAddress,
    string BroadcastAddress,
    string FirstHost,
    string LastHost,
    string HostCount,
    string Cidr);

/// <summary>
/// 功能职责：由 IP + CIDR/掩码计算网络地址、广播地址、主机范围。不负责 IPv6。
/// 方法群组：计算子网。
/// </summary>
public static class IpCalculatorHelp
{
    /// <summary>
    /// 功能作用：计算子网信息。支持 192.168.1.10/24、192.168.1.10 255.255.255.0、纯 IP（/32）。
    /// </summary>
    /// <param name="input">IP 与掩码/CIDR 文本。</param>
    /// <returns>成功时携带子网字段；无法解析则 Fail。</returns>
    public static Result<IpCalculatorResult> Calculate(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
            return Result<IpCalculatorResult>.Fail("请输入 IPv4 地址，如 192.168.1.10/24 或 192.168.1.10 255.255.255.0。");

        try
        {
            if (!TryParseInput(text, out var ip, out var prefix))
                return Result<IpCalculatorResult>.Fail("无法解析 IPv4 或子网掩码。");

            var mask = PrefixToMask(prefix);
            var wildcard = ~mask;
            var network = ip & mask;
            var broadcast = ip | wildcard;

            var address = UInt32ToIp(ip);
            var integerValue = ip.ToString(CultureInfo.InvariantCulture);
            var hexValue = "0x" + ip.ToString("X8", CultureInfo.InvariantCulture);
            var subnetMask = UInt32ToIp(mask);
            var wildcardMask = UInt32ToIp(wildcard);
            var networkAddress = UInt32ToIp(network);
            var broadcastAddress = UInt32ToIp(broadcast);
            var cidr = $"/{prefix}";

            string firstHost;
            string lastHost;
            string hostCount;
            var total = prefix >= 31 ? (uint)(1u << (32 - prefix)) : (uint)((1L << (32 - prefix)) - 2);
            if (prefix >= 31)
            {
                firstHost = prefix == 32 ? address : UInt32ToIp(network);
                lastHost = prefix == 32 ? address : UInt32ToIp(broadcast);
                hostCount = prefix == 32 ? "1（单主机）" : "2（/31 点对点）";
            }
            else
            {
                firstHost = UInt32ToIp(network + 1);
                lastHost = UInt32ToIp(broadcast - 1);
                hostCount = total.ToString(CultureInfo.InvariantCulture);
            }

            return Result<IpCalculatorResult>.Ok(new IpCalculatorResult(
                address, integerValue, hexValue, subnetMask, wildcardMask,
                networkAddress, broadcastAddress, firstHost, lastHost, hostCount, cidr));
        }
        catch (Exception ex)
        {
            return Result<IpCalculatorResult>.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 解析输入的 IP/前缀组合：CIDR（a.b.c.d/n）、IP + 掩码（空格分隔）或纯 IP（/32）。
    /// </summary>
    private static bool TryParseInput(string text, out uint ip, out int prefix)
    {
        ip = 0;
        prefix = 0;

        if (text.Contains('/'))
        {
            var parts = text.Split('/', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TryParseIp(parts[0], out ip) || !int.TryParse(parts[1], out prefix))
                return false;
            return prefix is >= 0 and <= 32;
        }

        var tokens = text.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 1)
        {
            if (!TryParseIp(tokens[0], out ip)) return false;
            prefix = 32;
            return true;
        }

        if (tokens.Length != 2 || !TryParseIp(tokens[0], out ip))
            return false;

        if (tokens[1].StartsWith('/'))
            return int.TryParse(tokens[1][1..], out prefix) && prefix is >= 0 and <= 32;

        if (!TryParseIp(tokens[1], out var maskIp))
            return false;

        prefix = MaskToPrefix(maskIp);
        return prefix >= 0;
    }

    /// <summary>
    /// IPv4 字符串解析为 uint（按网络字节序，最高位为第一段）。
    /// </summary>
    private static bool TryParseIp(string text, out uint value)
    {
        value = 0;
        if (!IPAddress.TryParse(text, out var addr) || addr.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var bytes = addr.GetAddressBytes();
        value = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        return true;
    }

    /// <summary>
    /// CIDR 前缀长度（0–32）转点分十进制掩码。
    /// </summary>
    private static uint PrefixToMask(int prefix) =>
        prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);

    /// <summary>
    /// 点分十进制掩码转前缀长度；非连续掩码（如 255.0.255.0）抛 FormatException。
    /// </summary>
    private static int MaskToPrefix(uint mask)
    {
        var seenZero = false;
        var prefix = 0;
        for (var i = 31; i >= 0; i--)
        {
            var bit = (mask >> i) & 1;
            if (bit == 1)
            {
                if (seenZero) throw new FormatException("子网掩码无效。");
                prefix++;
            }
            else seenZero = true;
        }
        return prefix;
    }

    /// <summary>
    /// uint（网络字节序）转 IPv4 点分十进制字符串。
    /// </summary>
    private static string UInt32ToIp(uint value)
    {
        var bytes = new byte[]
        {
            (byte)(value >> 24),
            (byte)(value >> 16),
            (byte)(value >> 8),
            (byte)value
        };
        return new IPAddress(bytes).ToString();
    }
}
