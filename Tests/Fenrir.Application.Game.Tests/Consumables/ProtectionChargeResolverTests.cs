using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class ProtectionChargeResolverTests
{
    [Fact]
    public void ResolveCharmCharge_SingleUnit_AddsThePerUnitAmount()
    {
        var result = ProtectionChargeResolver.ResolveCharmCharge(10, 1, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(11, result.NewCounterValue);
        Assert.Equal(1, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveCharmCharge_BulkUnits_MultipliesThePerUnitAmount()
    {
        var result = ProtectionChargeResolver.ResolveCharmCharge(0, 5, 3);

        Assert.True(result.Succeeded);
        Assert.Equal(15, result.NewCounterValue);
        Assert.Equal(3, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveCharmCharge_WouldExceedCeiling_Rejects_ConsumesNothing()
    {
        var result = ProtectionChargeResolver.ResolveCharmCharge(BankedCounterMath.GlobalCeiling - 1,
            5, 3);

        Assert.Equal(ProtectionChargeResolver.ChargeOutcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(0, result.UnitsConsumed);
        Assert.Equal(BankedCounterMath.GlobalCeiling - 1, result.NewCounterValue);
    }

    [Fact]
    public void ResolveCpProtCharmCharge_HaloRankBelowThreshold_Succeeds()
    {
        var result = ProtectionChargeResolver.ResolveCpProtCharmCharge(0, 3,
            1, ProtectionChargeResolver.HaloRankGateThreshold - 1);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.NewCounterValue);
    }

    [Fact]
    public void ResolveCpProtCharmCharge_HaloRankAtThreshold_Rejects()
    {
        var result = ProtectionChargeResolver.ResolveCpProtCharmCharge(0, 3,
            1, ProtectionChargeResolver.HaloRankGateThreshold);

        Assert.Equal(ProtectionChargeResolver.ChargeOutcome.HaloRankTooHigh, result.Outcome);
        Assert.Equal(0, result.NewCounterValue);
    }

    [Fact]
    public void ResolveCpProtCharmCharge_HaloRankAboveThreshold_Rejects()
    {
        var result = ProtectionChargeResolver.ResolveCpProtCharmCharge(0, 3,
            1, ProtectionChargeResolver.HaloRankGateThreshold + 10);

        Assert.Equal(ProtectionChargeResolver.ChargeOutcome.HaloRankTooHigh, result.Outcome);
    }

    [Fact]
    public void ResolveScrollCharge_SingleUnitOnly_AddsTheFixedAmount()
    {
        var result = ProtectionChargeResolver.ResolveScrollCharge(10, 180);

        Assert.True(result.Succeeded);
        Assert.Equal(190, result.NewCounterValue);
        Assert.Equal(1, result.UnitsConsumed);
    }

    [Fact]
    public void ResolveScrollCharge_WouldExceedCeiling_Rejects()
    {
        var result = ProtectionChargeResolver.ResolveScrollCharge(BankedCounterMath.GlobalCeiling,
            1);

        Assert.Equal(ProtectionChargeResolver.ChargeOutcome.WouldExceedCeiling, result.Outcome);
    }
}
