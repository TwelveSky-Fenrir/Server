using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

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
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, pipe) = SetUp(WinningTribe, progress);
        ZoneTestKit.DrainOutbound(pipe);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);

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
        var progress = new QuestProgress(3, 1, 8, Zone038MapId, 0);
        var (zone, state, _) = SetUp(WinningTribe, progress);
        Assert.Equal(0, state.MissionJoinWar);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, state.QuestKillCounter);
        Assert.Equal(0, state.MissionJoinWar);
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
        var progress = new QuestProgress(3, 1, 1, Zone038MapId, 0);
        var (zone, state, _) = SetUp(WinningTribe, progress);

        zone.Post(ZoneCommand.CreditZone038Occupation(1, WinningTribe));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.QuestKillCounter);
    }

    [Fact]
    public void Archetype8Quest_TargetingADifferentZone_IsNotCredited()
    {
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
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.False(zone.TryGetPlayer(999, out _));
    }
}
