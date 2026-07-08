using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World.Loot;

/// <summary>
///     Covers ground-item lifecycle end-to-end through <see cref="Zone" />'s public surface: spawning via a
///     guaranteed monster-kill drop, claiming (<see cref="Zone.TryClaimGroundItem" />), and the 60 s expiry sweep.
/// </summary>
public class ZoneGroundItemTests
{
    private const int PotionItemId = 8001;

    private static Zone CreateZoneWithGuaranteedPotionDrop(out int killerCharacterId)
    {
        var monster = WorldDataTestRows.Monster(800) with
        {
            Life = 10,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 800) with
        {
            Number = 1,
            LocationX = 50,
            LocationY = 0,
            LocationZ = 50,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with
        {
            Monsters = [monster],
            MonsterSpawnRegions = [region],
            MonsterDropPotions = [new MonsterDropPotionRowDto(800, 0, 1_000_000, PotionItemId)]
        };
        var cache = WorldDataCacheBuilder.Build(rows).Cache;

        var scheduler = new MonsterSpawnScheduler(cache, static () => new MaxValueRandom());
        var zone = ZoneTestKit.CreateZone(1, simulationSystems: [scheduler], worldData: cache);

        var (session, _) = ZoneTestKit.CreateSession(1);
        killerCharacterId = 20;
        zone.Post(ZoneCommand.Enter(killerCharacterId,
            ZoneTestKit.EnterData(session, 1, "Looter", 50, posZ: 50,
                level: 1))); // matches monster.ItemLevel (drop eligibility gap <= 9)
        zone.Tick(SimulationClock.LegacyTick); // enters + pops the monster

        Assert.True(zone.TryGetMonster(1, out var monster1));
        zone.TryDamageMonster(1, 10_000, killerCharacterId, out var died, out _);
        Assert.True(died);
        zone.Tick(SimulationClock.LegacyTick); // drains the death: rolls the guaranteed potion drop

        Assert.Equal(1, zone.GroundItemCount);
        return zone;
    }

    [Fact]
    public void KillerCanClaim_ImmediatelyAtTheDropSpot()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out var killerId);

        var outcome = zone.TryClaimGroundItem(1, 1u, "Looter", null, 50, 0, 50, out var item);

        Assert.Equal(GroundItemClaimOutcome.Success, outcome);
        Assert.Equal(PotionItemId, item!.ItemId);
        Assert.Equal(0, zone.GroundItemCount);
        _ = killerId;
    }

    [Fact]
    public void OtherPlayer_CannotClaim_BeforeFreeForAllWindow()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        var outcome = zone.TryClaimGroundItem(1, 1u, "SomeoneElse", null, 50, 0, 50, out var item);

        Assert.Equal(GroundItemClaimOutcome.NotOwned, outcome);
        Assert.Null(item);
        Assert.Equal(1, zone.GroundItemCount); // never removed on a rejected claim
    }

    [Fact]
    public void Claim_TooFarAway_IsRejected_ItemRemains()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        var outcome = zone.TryClaimGroundItem(1, 1u, "Looter", null, 50 + 1000, 0, 50, out var item);

        Assert.Equal(GroundItemClaimOutcome.TooFar, outcome);
        Assert.Null(item);
        Assert.Equal(1, zone.GroundItemCount);
    }

    [Fact]
    public void Claim_WrongUniqueNumber_IsRejected_AsIfNotFound()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        var outcome = zone.TryClaimGroundItem(1, 999u, "Looter", null, 50, 0, 50, out var item);

        Assert.Equal(GroundItemClaimOutcome.NotFound, outcome);
        Assert.Null(item);
    }

    [Fact]
    public void Claim_UnknownServerIndex_IsRejected_AsNotFound()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        var outcome = zone.TryClaimGroundItem(999, 1u, "Looter", null, 50, 0, 50, out var item);

        Assert.Equal(GroundItemClaimOutcome.NotFound, outcome);
        Assert.Null(item);
    }

    [Fact]
    public void OtherPlayer_CanClaim_OnceFreeForAllWindowPasses()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        // Advance the zone clock past the 30 s free-for-all window (LegacyTick = 500 ms per Tick call).
        for (var i = 0; i < 61; i++) // 61 * 0.5s = 30.5s
            zone.Tick(TimeSpan.FromMilliseconds(500));

        var outcome = zone.TryClaimGroundItem(1, 1u, "SomeoneElse", null, 50, 0, 50, out var item);

        Assert.Equal(GroundItemClaimOutcome.Success, outcome);
        Assert.NotNull(item);
    }

    [Fact]
    public void Item_Expires_AndDisappearsFromThePool_AfterSixtySeconds()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        for (var i = 0; i < 130; i++) // 130 * 0.5s = 65s > 60s lifetime
            zone.Tick(TimeSpan.FromMilliseconds(500));

        Assert.Equal(0, zone.GroundItemCount);
    }

    [Fact]
    public void ConcurrentClaimAttempts_ExactlyOneSucceeds()
    {
        var zone = CreateZoneWithGuaranteedPotionDrop(out _);

        // dedicated OS threads released via a barrier, not Parallel.For -- the thread pool could stagger starts enough to never actually race
        const int attempts = 20;
        var outcomes = new GroundItemClaimOutcome[attempts];
        var exceptions = new Exception?[attempts];
        using var barrier = new Barrier(attempts);
        var threads = new Thread[attempts];

        for (var i = 0; i < attempts; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                barrier.SignalAndWait();
                try
                {
                    outcomes[index] = zone.TryClaimGroundItem(1, 1u, "Looter", null, 50, 0, 50, out _);
                }
                catch (Exception ex)
                {
                    exceptions[index] = ex;
                }
            });
            threads[index].Start();
        }

        foreach (var thread in threads)
            thread.Join();

        Assert.All(exceptions, ex => Assert.Null(ex));

        Assert.Equal(1, outcomes.Count(o => o == GroundItemClaimOutcome.Success));
        Assert.Equal(0, zone.GroundItemCount);
    }

    /// <summary>
    ///     Covers <c>Zone.BroadcastGroundItemAction</c>'s <see cref="AoiGrid.HasAnyNeighbor" /> emptiness
    ///     pre-check, the ground-item counterpart of the same refactor already covered for monsters
    ///     (<c>ZoneMonsterAoiGridTests</c>) and proxy shops (<c>ZoneProxyShopTests.ShopWithNoNeighbors_IsNeverBroadcastTo</c>
    ///     ):
    ///     a drop with nobody nearby must neither throw nor send anything, while a drop next to a player must
    ///     still reach them exactly as before.
    /// </summary>
    [Fact]
    public void SpawnGroundItem_WithNoNeighborsAnywhere_SendsNothing_AndDoesNotThrow()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.SpawnGroundItem(PotionItemId, 1, 5000f, 0f, 5000f, "Nobody", "", 0);

        Assert.Equal(1, zone.GroundItemCount);
    }

    [Fact]
    public void SpawnGroundItem_BroadcastsTheCreationFrame_ToANearbyPlayer()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe) = ZoneTestKit.CreateSession(1);

        // Same AOI cell as the drop below (default cell size, both floor to the same coarse cell).
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, posX: 50f, posZ: 50f)));
        zone.Tick(SimulationClock.LegacyTick);
        ZoneTestKit.DrainOutbound(pipe);

        zone.SpawnGroundItem(PotionItemId, 1, 60f, 0f, 60f, "Someone", "", 0);

        Assert.Equal(FrameWriter.FrameSizeOf<GroundItemReplicationResponse>(),
            ZoneTestKit.DrainOutbound(pipe).Length);
    }

    /// <summary>
    ///     Always draws the maximum value, so the guaranteed potion drop succeeds and the unconditional item-864 roll
    ///     (threshold 1000/1,000,000) stays unreachable.
    /// </summary>
    private sealed class MaxValueRandom : Random
    {
        public override int Next(int minValue, int maxValue)
        {
            return Math.Max(minValue, maxValue - 1);
        }
    }
}
