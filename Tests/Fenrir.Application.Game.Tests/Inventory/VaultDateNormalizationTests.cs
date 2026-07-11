using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class VaultDateNormalizationTests
{
    [Fact]
    public void StrictlyEarlierThanToday_CollapsesToZeroSentinel()
    {
        Assert.Equal(0, VaultDateNormalization.NormalizeIfExpired(20200101, 20260710));
    }

    [Fact]
    public void ExactlyToday_LeftUntouched()
    {
        Assert.Equal(20260710, VaultDateNormalization.NormalizeIfExpired(20260710, 20260710));
    }

    [Fact]
    public void StrictlyLaterThanToday_LeftUntouched()
    {
        Assert.Equal(20261231, VaultDateNormalization.NormalizeIfExpired(20261231, 20260710));
    }

    [Fact]
    public void AlreadyZeroSentinel_StaysZero()
    {
        Assert.Equal(0, VaultDateNormalization.NormalizeIfExpired(0, 20260710));
    }
}
