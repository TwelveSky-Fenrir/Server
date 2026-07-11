using System.Net;

namespace Fenrir.Application.Game.Domain.AntiCheat;

/// <summary>
///     Normalizes a session's peer address into the canonical string used for the PvP same-origin kill guard
///     (<see cref="PvpKillCreditGuard" />). Legacy captures a raw <c>mIP</c> text at connect time
///     (<c>Server/ts25zone/S07_MyGame03.cpp:2727,2827</c>) and compares two sessions' strings with an exact
///     <c>IsMatchString</c> equality (<c>:2827-2830</c>). Fenrir must likewise carry a per-session source IP —
///     see <c>PlayerRuntimeState.SourceIp</c> — captured from the accepted socket's remote endpoint at world
///     entry.
/// </summary>
/// <remarks>
///     Hardening over legacy's naive string compare: an IPv4-mapped IPv6 address (<c>::ffff:a.b.c.d</c>) is
///     unwrapped to its plain IPv4 form so the two spellings of the same host compare equal. Legacy's raw
///     <c>strcmp</c>-style match would treat them as different hosts and let a dual-stack client sidestep the
///     same-IP guard. This does NOT close the deeper weakness the D2 contract flags (two clients on genuinely
///     different IPs — separate machines, NAT, VPN — defeat any IP-only check); that is addressed by folding an
///     account-identity signal into <see cref="PvpKillCreditGuard.IsSameOrigin" />, not here.
/// </remarks>
public static class SessionSourceIp
{
    /// <summary>Canonical source-IP string for a session's remote endpoint, or null when the transport has no accepted socket.</summary>
    public static string? Normalize(IPEndPoint? endPoint)
    {
        return Normalize(endPoint?.Address);
    }

    /// <summary>Canonical source-IP string for an address, unwrapping IPv4-mapped IPv6; null passes through as null.</summary>
    public static string? Normalize(IPAddress? address)
    {
        if (address is null)
            return null;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return address.ToString();
    }

    /// <summary>
    ///     True only when both operands are present and equal. Two unknown (null/empty) IPs never prove a shared
    ///     origin — an absent source IP must fail open (distinct hosts), never falsely block a legitimate kill.
    /// </summary>
    public static bool AreSameHost(string? left, string? right)
    {
        return !string.IsNullOrEmpty(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
