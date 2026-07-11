using System.Net;

namespace Fenrir.Application.Game.Domain.AntiCheat;

public static class SessionSourceIp
{
    public static string? Normalize(IPEndPoint? endPoint)
    {
        return Normalize(endPoint?.Address);
    }

    public static string? Normalize(IPAddress? address)
    {
        if (address is null)
            return null;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.ToString();
    }

    public static bool AreSameHost(string? left, string? right)
    {
        return !string.IsNullOrEmpty(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
