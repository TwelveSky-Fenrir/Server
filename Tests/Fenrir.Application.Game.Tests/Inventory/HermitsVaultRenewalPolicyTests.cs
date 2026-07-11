using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Tests.Inventory;

public class HermitsVaultRenewalPolicyTests
{
    [Fact]
    public void QualifyingItemIds_ContainsExactlyTheSevenCitedIds()
    {
        var expected = new[] { 807, 1102, 1129, 1141, 2019, 1356, 8407 };

        Assert.Equal(expected.Length, HermitsVaultRenewalPolicy.QualifyingItemIds.Count);
        foreach (var id in expected)
            Assert.Contains(id, HermitsVaultRenewalPolicy.QualifyingItemIds);
    }

    [Fact]
    public void CurrentExpiryStillValid_StacksForwardFromCurrentExpiry_NotFromToday()
    {
        var succeeded =
            HermitsVaultRenewalPolicy.TryComputeRenewedExpiry(20260715, 20260710, 30, out var newExpiry);

        Assert.True(succeeded);
        Assert.Equal(20260814, newExpiry);
    }

    [Fact]
    public void CurrentExpiryExactlyToday_TreatedAsStillValid_StacksForwardFromToday()
    {
        var succeeded =
            HermitsVaultRenewalPolicy.TryComputeRenewedExpiry(20260710, 20260710, 7, out var newExpiry);

        Assert.True(succeeded);
        Assert.Equal(20260717, newExpiry);
    }

    [Fact]
    public void CurrentExpiryAlreadyLapsed_BaselinesFromToday_NotFromTheStaleDate()
    {
        var succeeded =
            HermitsVaultRenewalPolicy.TryComputeRenewedExpiry(20200101, 20260710, 60, out var newExpiry);

        Assert.True(succeeded);
        Assert.Equal(20260908, newExpiry);
    }

    [Fact]
    public void CurrentExpiryZeroSentinel_BaselinesFromToday()
    {
        var succeeded = HermitsVaultRenewalPolicy.TryComputeRenewedExpiry(0, 20260710, 180, out var newExpiry);

        Assert.True(succeeded);
        Assert.Equal(20270106, newExpiry);
    }

    [Fact]
    public void NegativeDayCount_Fails_LeavingNoUsableExpiry()
    {
        var succeeded =
            HermitsVaultRenewalPolicy.TryComputeRenewedExpiry(20260710, 20260710, -1, out var newExpiry);

        Assert.False(succeeded);
        Assert.Equal(GameDate.Invalid, newExpiry);
    }
}
