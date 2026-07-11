using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.RecordEnemyKillForFeed" />/<see cref="Zone.ApplyKillFeedEndOfBattleRewards" />/
///     <see cref="Zone.ClearKillFeedLeaderboard" /> directly -- the kill-site hook itself is not yet wired
///     into <c>ApplyCombatCommand</c>/the reflect-kill path (see this mechanic's own wiring manifest), so
///     these tests call the new public methods straight, mirroring how <c>ZonePvpKillRewardsTests</c> already
///     exercises <c>Zone</c>'s reward pipeline against directly-constructed player state.
/// </summary>
public class ZoneKillFeedTests
{
    private static int OneFrame => FrameWriter.FrameSizeOf<ZoneEventInfoResponse>();

    private static (Zone Zone, FakeDuplexPipe KillerPipe, FakeDuplexPipe VictimPipe) SetUpZone(short mapId,
        IFourGuildKillPointQueue? fourGuildKillPointQueue = null)
    {
        var zone = ZoneTestKit.CreateZone(mapId, fourGuildKillPointQueue: fourGuildKillPointQueue);

        var (killerSession, killerPipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(killerSession, mapId, "Killer", tribe: 0)));

        var (victimSession, victimPipe) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(victimSession, mapId, "Victim", tribe: 1)));

        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Drain the enter-broadcast frames so each test's own assertions only see the kill-feed frame itself.
        ZoneTestKit.DrainOutbound(killerPipe);
        ZoneTestKit.DrainOutbound(victimPipe);

        return (zone, killerPipe, victimPipe);
    }

    [Fact]
    public void NonWarZone_NoCounterIncrement_NoBroadcast()
    {
        var (zone, killerPipe, _) = SetUpZone(1); // 1 is not a modeled war-zone type
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Equal(0, killer!.SessionKillCount);
        Assert.Empty(ZoneTestKit.DrainOutbound(killerPipe));
    }

    [Fact]
    public void WarZoneActive_IncrementsSessionKillCount_AndBroadcastsWithRealTopThree()
    {
        var (zone, killerPipe, _) = SetUpZone(49); // Zone049-type
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Equal(1, killer!.SessionKillCount);

        var frame = ZoneTestKit.DrainOutbound(killerPipe);
        Assert.Equal(OneFrame, frame.Length);

        var payload = frame.AsSpan(1);
        var sort = BinaryPrimitives.ReadInt32LittleEndian(payload);
        Assert.Equal(3000, sort);

        var data = payload[4..];
        var killerName = ReadFixedName(data, 0);
        Assert.Equal("Killer", killerName);

        // Top-1 slot starts at offset 28 within Data -- see KillFeedBroadcastEncoderTests for the layout math.
        var top1Name = ReadFixedName(data, 28);
        var top1Kills = BinaryPrimitives.ReadInt32LittleEndian(data[(28 + 14)..]);
        Assert.Equal("Killer", top1Name);
        Assert.Equal(1, top1Kills);
    }

    [Fact]
    public void WarZoneInactive_StillBroadcasts_ButBlankTopThree_AndNoCounterIncrement()
    {
        var (zone, killerPipe, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: false);

        Assert.Equal(0, killer!.SessionKillCount);

        var frame = ZoneTestKit.DrainOutbound(killerPipe);
        Assert.Equal(OneFrame, frame.Length);

        var data = frame.AsSpan(1)[4..];
        var top1Name = ReadFixedName(data, 28);
        var top1Kills = BinaryPrimitives.ReadInt32LittleEndian(data[(28 + 14)..]);
        Assert.Equal(" ", top1Name);
        Assert.Equal(0, top1Kills);
    }

    [Fact]
    public void StunTrigger_NoBroadcast_NoCounterIncrement()
    {
        var (zone, killerPipe, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: true, warStateActive: true);

        Assert.Equal(0, killer!.SessionKillCount);
        Assert.Empty(ZoneTestKit.DrainOutbound(killerPipe));
    }

    [Fact]
    public void FfaZoneActive_GrantsWarPointsAndBloodPoints()
    {
        var (zone, _, _) = SetUpZone(KillFeedZoneCatalog.FfaMapNumber);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Equal(KillFeedRewardConstants.FfaWarPointPerKill, killer!.WarPoint);
        Assert.Equal(KillFeedRewardConstants.FfaBloodPointPerKill, killer.BloodCoin);
    }

    [Fact]
    public void FfaZoneActive_RepeatedKillWithinCooldown_GrantsPointsOnlyOnce()
    {
        var (zone, _, _) = SetUpZone(KillFeedZoneCatalog.FfaMapNumber);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);
        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Equal(KillFeedRewardConstants.FfaWarPointPerKill, killer!.WarPoint);
        Assert.Equal(KillFeedRewardConstants.FfaBloodPointPerKill, killer.BloodCoin);
    }

    [Fact]
    public void NonFfaWarZone_NoWarPointOrBloodPointAward()
    {
        var (zone, _, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Equal(0, killer!.WarPoint);
        Assert.Equal(0, killer.BloodCoin);
    }

    [Fact]
    public void ApplyKillFeedEndOfBattleRewards_CreditsTopKillerContributionPoints()
    {
        var (zone, _, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);
        zone.ApplyKillFeedEndOfBattleRewards(isFfaMap: false, isZone267: false);

        Assert.Equal(KillFeedRewardConstants.NonFfaTop1ContributionPoints, killer!.ContributionPoints);
    }

    [Fact]
    public void ApplyKillFeedEndOfBattleRewards_PlayerNoLongerPresent_DoesNotThrow_AndGrantsNothingToThem()
    {
        var (zone, _, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);

        zone.Post(ZoneCommand.Leave(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // The killer entity object we hold is now detached from the zone's own _players map; the exercise here
        // is that end-of-battle reward application does not throw for a leaderboard entry whose character has
        // since left, and the still-present victim (never a leaderboard entry, 0 kills) is left untouched.
        zone.ApplyKillFeedEndOfBattleRewards(isFfaMap: false, isZone267: false);

        Assert.Equal(0, victim!.ContributionPoints);
    }

    [Fact]
    public void ApplyKillFeedEndOfBattleRewards_FfaMap_UsesFfaCpTable()
    {
        var (zone, _, _) = SetUpZone(KillFeedZoneCatalog.FfaMapNumber);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);
        zone.ApplyKillFeedEndOfBattleRewards(isFfaMap: true, isZone267: false);

        Assert.Equal(KillFeedRewardConstants.FfaTop1ContributionPoints, killer!.ContributionPoints);
    }

    [Fact]
    public void ClearKillFeedLeaderboard_ResetsLeaderboard_ButNotSessionKillCount()
    {
        var (zone, _, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));

        zone.RecordEnemyKillForFeed(killer!, victim!, isStunTrigger: false, warStateActive: true);
        Assert.Equal(1, killer!.SessionKillCount);

        zone.ClearKillFeedLeaderboard();

        // The counter survives the clear (source contract: "does NOT reset any player's session kill counter").
        Assert.Equal(1, killer.SessionKillCount);

        // End-of-battle reward after a clear with no new kills recorded grants nothing (leaderboard is empty).
        zone.ApplyKillFeedEndOfBattleRewards(isFfaMap: false, isZone267: false);
        Assert.Equal(0, killer.ContributionPoints);
    }

    [Fact]
    public void ClearKillFeedLeaderboard_NonWarZone_NoOp_DoesNotThrow()
    {
        var (zone, _, _) = SetUpZone(1);

        zone.ClearKillFeedLeaderboard();
        zone.ApplyKillFeedEndOfBattleRewards(isFfaMap: false, isZone267: false);
    }

    // C17 Part B1: four-guild per-kill point accrual, gated to Zone049-type maps only and independent of
    // warStateActive/leaderboard eligibility (see RecordEnemyKillForFeed's own remarks on this call).

    [Fact]
    public void Zone049Type_KillerHasGuild_EnqueuesExactlyOnePointForKillersGuild()
    {
        var queue = new FakeFourGuildKillPointQueue();
        var (zone, _, _) = SetUpZone(49, queue); // Zone049-type
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        killer!.GuildId = 77;

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Equal([77], queue.EnqueuedGuildIds);
    }

    [Fact]
    public void Zone049Type_KillerHasNoGuild_DoesNotEnqueue()
    {
        var queue = new FakeFourGuildKillPointQueue();
        var (zone, _, _) = SetUpZone(49, queue);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        Assert.Null(killer!.GuildId);

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Empty(queue.EnqueuedGuildIds);
    }

    [Fact]
    public void NonZone049TypeMap_KillerHasGuild_DoesNotEnqueue()
    {
        var queue = new FakeFourGuildKillPointQueue();
        var (zone, _, _) = SetUpZone(1, queue); // not a Zone049-type map
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        killer!.GuildId = 77;

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Empty(queue.EnqueuedGuildIds);
    }

    [Fact]
    public void FfaMap_KillerHasGuild_DoesNotEnqueue()
    {
        var queue = new FakeFourGuildKillPointQueue();
        var (zone, _, _) = SetUpZone(KillFeedZoneCatalog.FfaMapNumber, queue); // 335, not Zone049-type
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        killer!.GuildId = 77;

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: false, warStateActive: true);

        Assert.Empty(queue.EnqueuedGuildIds);
    }

    [Fact]
    public void Zone049Type_StunTrigger_DoesNotEnqueue()
    {
        var queue = new FakeFourGuildKillPointQueue();
        var (zone, _, _) = SetUpZone(49, queue);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        killer!.GuildId = 77;

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: true, warStateActive: true);

        Assert.Empty(queue.EnqueuedGuildIds);
    }

    [Fact]
    public void Zone049Type_WarStateInactive_StillEnqueues()
    {
        // The accrual gate is independent of warStateActive (see RecordEnemyKillForFeed's own remarks) --
        // unlike the leaderboard/session-kill-count increment, which IS gated on it.
        var queue = new FakeFourGuildKillPointQueue();
        var (zone, _, _) = SetUpZone(49, queue);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        killer!.GuildId = 77;

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: false, warStateActive: false);

        Assert.Equal([77], queue.EnqueuedGuildIds);
    }

    [Fact]
    public void Zone049Type_NullQueue_DoesNotThrow()
    {
        // Most call sites (every other test in this file) construct the zone with no queue at all -- the
        // real production default before ZoneRegistry/DI wires the singleton relay in.
        var (zone, _, _) = SetUpZone(49);
        Assert.True(zone.TryGetPlayer(1, out var killer));
        Assert.True(zone.TryGetPlayer(2, out var victim));
        killer!.GuildId = 77;

        zone.RecordEnemyKillForFeed(killer, victim!, isStunTrigger: false, warStateActive: true);
    }

    private static string ReadFixedName(ReadOnlySpan<byte> data, int offset)
    {
        var raw = System.Text.Encoding.ASCII.GetString(data.Slice(offset, 13));
        var nullIndex = raw.IndexOf('\0');
        return nullIndex >= 0 ? raw[..nullIndex] : raw;
    }

    private sealed class FakeFourGuildKillPointQueue : IFourGuildKillPointQueue
    {
        public List<int> EnqueuedGuildIds { get; } = [];

        public bool Enqueue(int guildId)
        {
            EnqueuedGuildIds.Add(guildId);
            return true;
        }
    }
}
