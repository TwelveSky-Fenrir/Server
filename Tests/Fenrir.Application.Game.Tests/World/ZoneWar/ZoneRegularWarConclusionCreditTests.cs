using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <c>Zone.HandleRegularWarConclusionCredit</c> -- the <see cref="ZoneCommandKind.CreditRegularWarConclusion" />
///     handler closing the "DailyMission claim's join-war precondition can never be satisfied" gap. The
///     posting side (<c>RegularWarSchedulerHost.CreditDailyMissionAndWaterfallQuest</c>) is covered by
///     <see cref="RegularWarSchedulerHostTests" />; this file only exercises the Zone-side mutation in
///     isolation, same split as <c>ZonePvpKillMissionProgressTests</c> covers the C05 side of the sibling
///     <see cref="PlayerRuntimeState.MissionKillOtherTribe" /> hook.
/// </summary>
public class ZoneRegularWarConclusionCreditTests
{
    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) SetUp(short mapId = 49,
        QuestProgress questProgress = default)
    {
        var zone = ZoneTestKit.CreateZone(mapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var enterData = ZoneTestKit.EnterData(session, mapId) with { QuestProgress = questProgress };
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public void PresentPlayer_MissionJoinWar_IncrementsFromZeroToOne()
    {
        var (zone, state, _) = SetUp();
        Assert.Equal(0, state.MissionJoinWar);

        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.MissionJoinWar);
    }

    [Fact]
    public void MissionJoinWar_ClampsAtTheLegacyCap_RepeatCreditSameCycle()
    {
        var (zone, state, _) = SetUp();

        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.MissionJoinWar);
    }

    [Fact]
    public void CharacterNoLongerTracked_IsANoOp()
    {
        var zone = ZoneTestKit.CreateZone(49);

        zone.Post(ZoneCommand.CreditRegularWarConclusion(999));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw

        Assert.False(zone.TryGetPlayer(999, out _));
    }

    [Fact]
    public void Archetype8QuestHolder_WithMatchingZoneTarget_AdvancesKillCounter_AndPushesMarkerNine()
    {
        // qSort 8 (waterfall occupation), TargetPhase == this zone's own map id (49), still in progress
        // (KillCounter 0).
        var progress = new QuestProgress(StepPermanent: 3, ActiveFlag: 1, QSort: 8, TargetPhase: 49, KillCounter: 0);
        var (zone, state, pipe) = SetUp(49, progress);
        ZoneTestKit.DrainOutbound(pipe); // discard the Enter-time traffic

        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);

        // Wire shape: 1-byte opcode header, then the QuestProgressResponse payload (Sort/Page/Index/XPost/
        // YPost, 5 little-endian int32s -- see QuestProgressResponseTests' own golden-bytes encoding).
        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(9, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public void Archetype8QuestHolder_AlreadyAtCap_DoesNotDoubleCreditOrPushAgain()
    {
        var progress = new QuestProgress(StepPermanent: 3, ActiveFlag: 1, QSort: 8, TargetPhase: 49, KillCounter: 1);
        var (zone, state, pipe) = SetUp(49, progress);
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NonArchetype8Quest_NeverAdvancesKillCounter()
    {
        // qSort 1 (kill-monster) -- a different archetype entirely; must be untouched by this hook.
        var progress = new QuestProgress(StepPermanent: 3, ActiveFlag: 1, QSort: 1, TargetPhase: 49, KillCounter: 0);
        var (zone, state, _) = SetUp(49, progress);

        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
    }

    [Fact]
    public void Archetype8Quest_TargetingADifferentZone_IsNotCredited()
    {
        // TargetPhase (146) does not match this zone's own map id (49).
        var progress = new QuestProgress(StepPermanent: 3, ActiveFlag: 1, QSort: 8, TargetPhase: 146, KillCounter: 0);
        var (zone, state, _) = SetUp(49, progress);

        zone.Post(ZoneCommand.CreditRegularWarConclusion(1));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
    }
}
