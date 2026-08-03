using System.Net;
using System.Net.Sockets;

namespace Fenrir.Data.Runtime;

public static class SessionTicketBinding
{
    private const int Ipv4PrefixBits = 24;

    private const int Ipv6PrefixBits = 64;

    private const string LoopbackPrefix = "loopback";

    public static string? PrefixOf(IPAddress? address)
    {
        if (address is null)
            return null;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return LoopbackPrefix;

        Span<byte> octets = stackalloc byte[16];
        if (!address.TryWriteBytes(octets, out var written))
            return null;

        var prefixBits = address.AddressFamily == AddressFamily.InterNetwork ? Ipv4PrefixBits : Ipv6PrefixBits;

        for (var i = prefixBits / 8; i < written; i++)
            octets[i] = 0;

        return $"{new IPAddress(octets[..written])}/{prefixBits}";
    }
}
