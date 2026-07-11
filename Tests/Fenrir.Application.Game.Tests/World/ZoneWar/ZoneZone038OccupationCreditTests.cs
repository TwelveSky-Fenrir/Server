using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <c>Zone.HandleZone038OccupationCredit</c> -- the <see cref="ZoneCommandKind.CreditZone038Occupation" />
///     handler that applies the qSort-8 ("occupation of WaterFall") credit at the Holy Stone / Waterfall
///     (Zone038) war conclusion. The zone-local twin of <c>Zone.HandleRegularWarConclusionCredit</c>
///     (<see cref="ZoneRegularWarConclusionCreditTests" />), differing in two ways verified here: it is gated
///     on the winning-tribe/alive/non-transferring participant filter, and it does NOT touch
///     <see cref="PlayerRuntimeState.MissionJoinWar" /> (the Zone038 site carries no daily-mission "join war"
///     increment). The posting side (a future loop in <c>HolyStoneWarCycle.ResolveCapture</c>) is out of this
///     workstream's scope; this file exercises the Zone-side mutation in isolation.
/// </summary>
public class ZoneZone038OccupationCreditTests
{
    private const short Zone038MapId = 38;
    private const byte WinningTribe = 1;

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) SetUp(byte tribe = WinningTribe,
        QuestProgress questProgress = default)
    {
        var zone = ZoneTestKit.CreateZone(Zone038MapId);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var enterData = ZoneTestKit.EnterData(session, Zone038MapId, tribe: tribe) with
        {
            QuestProgress = questProgress
        };
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public void WinningTribeHolder_WithMatchingZoneTarget_AdvancesKillCounter_AndPushesMarkerNine()
    {
        // qSort 8, TargetPhase == this zone's own map id (38), still in progress (KillCounter 0).
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, pipe) = SetUp(WinningTribe, progress);
        ZoneTestKit.DrainOutbound(pipe); // discard the Enter-time traffic

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);

        // Wire shape: 1-byte opcode header, then the QuestProgressResponse payload (Sort/Page/Index/XPost/
        // YPost, 5 little-endian int32s).
        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(9, BinaryPrimitives.ReadInt32LittleEndian(payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[8..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[12..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(payload[16..]));
    }

    [Fact]
    public void Credit_DoesNotTouchMissionJoinWar()
    {
        // Distinguishing feature vs. HandleRegularWarConclusionCredit: the Zone038 credit site carries no
        // daily-mission "join war" increment.
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, _) = SetUp(WinningTribe, progress);
        Assert.Equal(0, state.MissionJoinWar);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);
        Assert.Equal(0, state.MissionJoinWar); // untouched
    }

    [Fact]
    public void AlreadyAtCap_DoesNotDoubleCreditOrPushAgain()
    {
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 1);
        var (zone, state, pipe) = SetUp(WinningTribe, progress);
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void LosingTribeMember_IsNotCredited()
    {
        // Character's tribe (2) differs from the winning tribe (1) -- skipped before the credit.
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, pipe) = SetUp(2, progress);
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void DeadCharacter_IsNotCredited()
    {
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, pipe) = SetUp(WinningTribe, progress);
        state.IsDead = true;
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void MidZoneTransferCharacter_IsNotCredited()
    {
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, pipe) = SetUp(WinningTribe, progress);
        state.IsMovingZone = true;
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NonArchetype8Quest_NeverAdvancesKillCounter()
    {
        // qSort 1 (kill-monster) -- a different archetype entirely; must be untouched by this hook.
        var progress = new QuestProgress(3, 1, 1, Zone038MapId, 0);
        var (zone, state, _) = SetUp(WinningTribe, progress);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
    }

    [Fact]
    public void Archetype8Quest_TargetingADifferentZone_IsNotCredited()
    {
        // TargetPhase (146) does not match this zone's own map id (38).
        var progress = new QuestProgress(3, 1, 8, 146, 0);
        var (zone, state, _) = SetUp(WinningTribe, progress);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
    }

    [Fact]
    public void CharacterNoLongerTracked_IsANoOp()
    {
        var zone = ZoneTestKit.CreateZone(Zone038MapId);

        zone.Post(ZoneCommand.CreditZone038Occupation(999, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // must not throw

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
