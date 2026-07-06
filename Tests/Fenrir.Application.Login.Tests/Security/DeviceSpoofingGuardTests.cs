using Fenrir.Application.Login.Domain.Security;

namespace Fenrir.Application.Login.Tests.Security;

// "Protect Spoofed" anti-spoofing gate (Server/ts25login/S08_MyDB.cpp:497-507): pure-logic coverage of
// DeviceSpoofingGuard in isolation, independent of LoginHandlerTests' end-to-end wiring assertions.
public class DeviceSpoofingGuardTests
{
    private const string RealMac = "11-22-33-44-55-66";
    private const string RealGuid = "{real-adapter-guid}";
    private const string RealIp = "203.0.113.50";

    private const string PlaceholderMac = "00-00-00-00-00-00";
    private const string PlaceholderGuid = "{0-0-0-0-0}";
    private const string PlaceholderIp = "127.0.0.1";

    [Fact]
    public void NonGmAccount_AllRealValues_IsNotSpoofed()
    {
        Assert.False(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, RealMac, RealGuid, RealIp));
    }

    [Fact]
    public void NonGmAccount_ZeroLengthMac_IsAlwaysSpoofed_EvenWithRealGuidAndIp()
    {
        // The legacy routine skips the entire real-value population block whenever the declared MAC is
        // zero-length, so the real GUID/IP the client declared are never even consulted here.
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, "", RealGuid, RealIp));
    }

    [Fact]
    public void NonGmAccount_MacEqualsPlaceholderLiteral_IsSpoofed()
    {
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, PlaceholderMac, RealGuid, RealIp));
    }

    [Fact]
    public void NonGmAccount_GuidEqualsPlaceholderLiteral_IsSpoofed()
    {
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, RealMac, PlaceholderGuid, RealIp));
    }

    [Fact]
    public void NonGmAccount_RemoteIpEqualsLoopback_IsSpoofed_EvenWithRealMacAndGuid()
    {
        // Real boundary condition per the contract: a legitimate same-host non-GM connection is
        // indistinguishable, by this gate, from a spoofed device tuple -- "any one" of the three trips it.
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, RealMac, RealGuid, PlaceholderIp));
    }

    [Fact]
    public void NonGmAccount_NullRemoteIp_IsNotSpoofedByIpAlone()
    {
        // A null observed remote IP (e.g. a unit test that never wires one up) simply never equals the
        // placeholder literal -- it does not itself trip the gate.
        Assert.False(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, RealMac, RealGuid, null));
    }

    [Fact]
    public void GmAccount_AllPlaceholders_IsNotSpoofed()
    {
        // Grade one or above is exempt entirely, even though the account row was just overwritten with
        // these exact placeholder values as a side effect of the same login attempt.
        Assert.False(DeviceSpoofingGuard.IsSpoofedDeviceTuple(1, PlaceholderMac, PlaceholderGuid, PlaceholderIp));
    }

    [Fact]
    public void GmAccount_ZeroLengthMac_IsNotSpoofed()
    {
        Assert.False(DeviceSpoofingGuard.IsSpoofedDeviceTuple(1, "", "", null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(short.MaxValue)]
    public void HigherGmGrades_AreAlsoExempt(int grade)
    {
        Assert.False(DeviceSpoofingGuard.IsSpoofedDeviceTuple(grade, PlaceholderMac, PlaceholderGuid, PlaceholderIp));
    }

    [Fact]
    public void NegativeGrade_IsTreatedAsNonGm_Unconfirmed()
    {
        // Not observed in the cited legacy range whether a negative grade is ever actually produced; the
        // "< 1" test would treat it the same as zero (non-GM) -- flagged as unconfirmed, not assumed, in the
        // behavior contract. Modeled here so the guard's own boundary behavior is at least pinned down.
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(-1, "", RealGuid, RealIp));
    }
}
