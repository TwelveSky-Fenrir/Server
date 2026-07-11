using Fenrir.Application.Login.Domain.Security;

namespace Fenrir.Application.Login.Tests.Security;

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
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, RealMac, RealGuid, PlaceholderIp));
    }

    [Fact]
    public void NonGmAccount_NullRemoteIp_IsNotSpoofedByIpAlone()
    {
        Assert.False(DeviceSpoofingGuard.IsSpoofedDeviceTuple(0, RealMac, RealGuid, null));
    }

    [Fact]
    public void GmAccount_AllPlaceholders_IsNotSpoofed()
    {
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
        Assert.True(DeviceSpoofingGuard.IsSpoofedDeviceTuple(-1, "", RealGuid, RealIp));
    }
}
