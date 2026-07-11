using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeGuardForceResetSweepTests
{
    private const int OrdinaryPoolBase = 1_000_000;
    private const int Zone038WinnerPoolBase = 1_001_000;

    private static Zone CreateZone()
    {
        return ZoneTestKit.CreateZone(2);
    }

    private static void SpawnGuardAt(Zone zone, int serverIndex)
    {
        var template = WorldDataTestRows.Monster(900) with { Type = 5, SpecialType = 7, Life = 100 };
        var entity = MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
            10f, 0f, 20f, 15f);
        zone.SpawnMonster(entity);
    }

    [Fact]
    public void Wipe_RemovesEveryLiveGuardInThePoolRegion_AndReturnsTheCount()
    {
        var zone = CreateZone();
        SpawnGuardAt(zone, OrdinaryPoolBase + TribeGuardRegionLayout.RelativeReservedIndex(0, 0));
        SpawnGuardAt(zone, OrdinaryPoolBase + TribeGuardRegionLayout.RelativeReservedIndex(1, 0));
        SpawnGuardAt(zone, OrdinaryPoolBase + TribeGuardRegionLayout.RelativeReservedIndex(3, 0));
        Assert.Equal(3, zone.MonsterCount);

        var wiped = TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolBase);

        Assert.Equal(3, wiped);
        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void Wipe_EmptyRegion_IsANoOp_ReturnsZero()
    {
        var zone = CreateZone();

        var wiped = TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolBase);

        Assert.Equal(0, wiped);
        Assert.Equal(0, zone.MonsterCount);
    }

    [Fact]
    public void Wipe_OnlyTouchesTheGivenPool_LeavesTheOtherFamilysGuardsAlive()
    {
        var zone = CreateZone();
        SpawnGuardAt(zone, OrdinaryPoolBase + 0);
        SpawnGuardAt(zone, Zone038WinnerPoolBase + 0);
        Assert.Equal(2, zone.MonsterCount);

        var wiped = TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolBase);

        Assert.Equal(1, wiped);
        Assert.Equal(1, zone.MonsterCount);
        Assert.True(zone.TryGetMonster(Zone038WinnerPoolBase + 0, out _));
        Assert.False(zone.TryGetMonster(OrdinaryPoolBase + 0, out _));
    }

    [Fact]
    public void Wipe_LeavesAnythingBeyondTheHundredSlotRegionUntouched()
    {
        var zone = CreateZone();
        SpawnGuardAt(zone, OrdinaryPoolBase + 0);
        SpawnGuardAt(zone, OrdinaryPoolBase + TribeGuardRegionLayout.RegionSlotCount);

        var wiped = TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolBase);

        Assert.Equal(1, wiped);
        Assert.True(zone.TryGetMonster(OrdinaryPoolBase + TribeGuardRegionLayout.RegionSlotCount, out _));
    }

    [Fact]
    public void Wipe_FollowedByFreshSpawn_ReplacesTheGuardWithANewUniqueNumber()
    {
        var zone = CreateZone();
        var serverIndex = OrdinaryPoolBase + 0;
        SpawnGuardAt(zone, serverIndex);
        Assert.True(zone.TryGetMonster(serverIndex, out var before));
        var beforeUnique = before!.UniqueNumber;

        TribeGuardForceResetSweep.Wipe(zone, OrdinaryPoolBase);
        Assert.Equal(0, zone.MonsterCount);

        SpawnGuardAt(zone, serverIndex);
        Assert.True(zone.TryGetMonster(serverIndex, out var after));
        Assert.NotEqual(beforeUnique, after!.UniqueNumber);
    }
}
