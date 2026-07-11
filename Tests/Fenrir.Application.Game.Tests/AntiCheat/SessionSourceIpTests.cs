using System.Net;
using Fenrir.Application.Game.Domain.AntiCheat;

namespace Fenrir.Application.Game.Tests.AntiCheat;

/// <summary>
///     Covers <see cref="SessionSourceIp" /> — the per-session source-IP normalizer/accessor feeding the PvP
///     same-origin kill guard.
/// </summary>
public class SessionSourceIpTests
{
    [Fact]
    public void Normalize_NullEndPoint_IsNull()
    {
        Assert.Null(SessionSourceIp.Normalize((IPEndPoint?)null));
        Assert.Null(SessionSourceIp.Normalize((IPAddress?)null));
    }

    [Fact]
    public void Normalize_PlainIpv4_RoundTripsCanonical()
    {
        var endPoint = new IPEndPoint(IPAddress.Parse("203.0.113.7"), 11000);
        Assert.Equal("203.0.113.7", SessionSourceIp.Normalize(endPoint));
    }

    [Fact]
    public void Normalize_Ipv4MappedIpv6_IsUnwrappedToPlainIpv4()
    {
        // ::ffff:203.0.113.7 must compare equal to the plain IPv4 spelling of the same host.
        var mapped = IPAddress.Parse("203.0.113.7").MapToIPv6();
        Assert.True(mapped.IsIPv4MappedToIPv6);

        Assert.Equal("203.0.113.7", SessionSourceIp.Normalize(mapped));
        Assert.Equal(SessionSourceIp.Normalize(IPAddress.Parse("203.0.113.7")), SessionSourceIp.Normalize(mapped));
    }

    [Fact]
    public void AreSameHost_TwoUnknowns_IsFalse()
    {
        // An absent source IP must never falsely prove a shared origin (fail open, distinct hosts).
        Assert.False(SessionSourceIp.AreSameHost(null, null));
        Assert.False(SessionSourceIp.AreSameHost("", ""));
        Assert.False(SessionSourceIp.AreSameHost("203.0.113.7", null));
    }

    [Fact]
    public void AreSameHost_EqualNormalizedStrings_IsTrue()
    {
        var mapped = SessionSourceIp.Normalize(IPAddress.Parse("203.0.113.7").MapToIPv6());
        var plain = SessionSourceIp.Normalize(new IPEndPoint(IPAddress.Parse("203.0.113.7"), 5));
        Assert.True(SessionSourceIp.AreSameHost(mapped, plain));
    }

    [Fact]
    public void AreSameHost_DifferentHosts_IsFalse()
    {
        Assert.False(SessionSourceIp.AreSameHost("203.0.113.7", "203.0.113.8"));
    }
}
