using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerUpgradeResolverTests
{
    private const int ZoneNumberForTower0 = 2;
    private const int ZoneNumberForBoundaryTower3 = 7;

    [Fact]
    public void WrongTribeRole_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(0, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void TowerNotValid_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 2, 201, false);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void RequestZoneMismatchesActualZone_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, 99,
            ZoneNumberForTower0, 0, 1, 2,
            201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void ZoneWithNoTower_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, 1, 1, 0,
            1, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void TowerOutsideCallersTribeBlock_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, 8, 8, 0,
            1, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void RequestedTypeOutOfRange_Disconnects(int requestedType)
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            requestedType, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void RequestedNextLevelRawNotOneOfTwoFourSix_Disconnects(int requestedNextLevelRaw)
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, requestedNextLevelRaw, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void CurrentLevelUntouchedTower_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 2, 0, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void CurrentLevelAlreadyMaxed_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 6, 801, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void CurrentTypeMismatchesRequestedType_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            2, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void RequestedNextLevelDoesNotFollowCurrentLevel_Disconnects()
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 4, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void EveryGatePassed_Succeeds_WithPackedNextState()
    {
        var result = TowerUpgradeResolver.Validate(1, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Success, result.Outcome);
        Assert.Equal(0, result.TowerIndex);
        Assert.Equal(401, result.NewPackedState);
    }

    [Fact]
    public void SubMasterRole_AlsoSucceeds()
    {
        var result = TowerUpgradeResolver.Validate(2, ZoneNumberForTower0, ZoneNumberForTower0, 0,
            1, 2, 201, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Success, result.Outcome);
    }

    [Fact]
    public void BoundaryTowerIndex_BelongsToBothAdjacentTribes()
    {
        var forTribe0 = TowerUpgradeResolver.Validate(1, ZoneNumberForBoundaryTower3,
            ZoneNumberForBoundaryTower3, 0, 3, 2,
            203, true);
        var forTribe1 = TowerUpgradeResolver.Validate(1, ZoneNumberForBoundaryTower3,
            ZoneNumberForBoundaryTower3, 1, 3, 2,
            203, true);

        Assert.Equal(TowerUpgradeResolver.Outcome.Success, forTribe0.Outcome);
        Assert.Equal(TowerUpgradeResolver.Outcome.Success, forTribe1.Outcome);
        Assert.Equal(3, forTribe0.TowerIndex);
        Assert.Equal(3, forTribe1.TowerIndex);
    }

    [Fact]
    public void TryFindMaterial_ScansPage0AscendingThenPage1()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(5, new ItemStack(666, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .Add(2, new ItemStack(666, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(0, new ItemStack(666, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var found = TowerUpgradeResolver.TryFindMaterial(page0, page1, 666, out var page, out var slot);

        Assert.True(found);
        Assert.Equal(ContainerMatrix.InventoryPage0, page);
        Assert.Equal(2, slot);
    }

    [Fact]
    public void TryFindMaterial_FallsBackToPage1WhenAbsentFromPage0()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty;
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty
            .Add(9, new ItemStack(1073, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0));

        var found = TowerUpgradeResolver.TryFindMaterial(page0, page1, 1073, out var page, out var slot);

        Assert.True(found);
        Assert.Equal(ContainerMatrix.InventoryPage1, page);
        Assert.Equal(9, slot);
    }

    [Fact]
    public void TryFindMaterial_NotPresentAnywhere_ReturnsFalse()
    {
        var empty = ImmutableDictionary<byte, ItemStack>.Empty;

        var found = TowerUpgradeResolver.TryFindMaterial(empty, empty, 666, out _, out _);

        Assert.False(found);
    }
}
