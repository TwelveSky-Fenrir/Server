using Fenrir.Application.Login.Domain.Security;

namespace Fenrir.Application.Login.Tests.Security;

public class PerAdapterLoginCapGateTests
{
    private const string RealMac = "11-22-33-44-55-66";

    [Fact]
    public void NonGmAccount_NoConfiguredRow_IsAllowed()
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, null));
    }

    [Fact]
    public void NonGmAccount_ConfiguredLimitBelowOne_IsOutrightBanned()
    {
        Assert.Equal(PerAdapterLoginCapOutcome.OutrightBanned,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, 0));
    }

    [Fact]
    public void NonGmAccount_ConfiguredLimitOfExactlyOne_IsConnectionLimitExceeded()
    {
        Assert.Equal(PerAdapterLoginCapOutcome.ConnectionLimitExceeded,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void NonGmAccount_ConfiguredLimitOfTwoOrMore_IsAllowed(int configuredAccountLimit)
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, configuredAccountLimit));
    }

    [Fact]
    public void NonGmAccount_ZeroLengthMac_IsAllowed_EvenWithAConfiguredBanRow()
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(0, "", 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(short.MaxValue)]
    public void GmAccount_ConfiguredLimitBelowOne_IsAllowed(int accountGrade)
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(accountGrade, RealMac, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void NonGmAccount_NoConfiguredRow_ConcurrentCountBelowImplicitDefaultLimit_IsAllowed(
        int concurrentDeviceSessionCount)
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, null, concurrentDeviceSessionCount));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void NonGmAccount_NoConfiguredRow_ConcurrentCountAtOrAboveImplicitDefaultLimit_IsConnectionLimitExceeded(
        int concurrentDeviceSessionCount)
    {
        Assert.Equal(PerAdapterLoginCapOutcome.ConnectionLimitExceeded,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, null, concurrentDeviceSessionCount));
    }

    [Fact]
    public void GmAccount_NoConfiguredRow_ConcurrentCountAtOrAboveImplicitDefaultLimit_IsAllowed()
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(1, RealMac, null, 5));
    }

    [Fact]
    public void NonGmAccount_ConfiguredLimitOfTwoOrMore_IgnoresConcurrentCount_IsAllowed()
    {
        Assert.Equal(PerAdapterLoginCapOutcome.Allowed,
            PerAdapterLoginCapGate.Evaluate(0, RealMac, 2, concurrentDeviceSessionCount: 999));
    }
}
