using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Quests;

public class ZoneQuestKillProgressTests
{
    private const short MapId = 49;
    private const int MonsterId = 500;

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) EnterCharacter(Zone zone,
        int characterId, QuestProgress questProgress, byte tribe = 1)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        var enterData = ZoneTestKit.EnterData(session, MapId, name: $"Char{characterId}", tribe: tribe) with
        {
            QuestProgress = questProgress
        };
        zone.Post(ZoneCommand.Enter(characterId, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return (zone, state!, pipe);
    }

    private static WorldDataCache WorldDataWithKillMonstersQuest(byte tribe, int solution2)
    {
        var quest = WorldDataTestRows.Quest(1) with
        {
            Category = (byte)(tribe + 1), Step = 3, Solution1 = MonsterId, Solution2 = solution2
        };
        var rows = WorldDataTestRows.MinimalRows() with { Quests = [quest], QuestRewards = [], QuestSpeeches = [] };
        var (cache, _) = WorldDataCacheBuilder.Build(rows);
        return cache;
    }

    [Fact]
    public void KillCaptainArchetype_KillerSolo_AdvancesCounter_AndPushesMarkerEight()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var progress = new QuestProgress(3, 1, 5, MonsterId, 0);
        var (_, killer, pipe) = EnterCharacter(zone, 1, progress);
        ZoneTestKit.DrainOutbound(pipe);

        zone.ApplyQuestKillProgress(1, MonsterId);

        Assert.Equal(1, killer.QuestKillCounter);
        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(payload));
    }

    [Fact]
    public void KillCaptainArchetype_KillerAlreadyAtCap_DoesNotDoubleCreditOrPushAgain()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var progress = new QuestProgress(3, 1, 5, MonsterId, 1);
        var (_, killer, pipe) = EnterCharacter(zone, 1, progress);
        ZoneTestKit.DrainOutbound(pipe);

        zone.ApplyQuestKillProgress(1, MonsterId);

        Assert.Equal(1, killer.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void KillCaptainArchetype_VisiblePartyMember_IsCredited_AndPushesMarkerEight()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        var (_, _, killerPipe) = EnterCharacter(zone, 1, QuestProgress.None);
        var (_, member, memberPipe) = EnterCharacter(zone, 2, new QuestProgress(3, 1, 5, MonsterId, 0));
        ZoneTestKit.DrainOutbound(killerPipe);
        ZoneTestKit.DrainOutbound(memberPipe);

        zone.ApplyQuestKillProgress(1, MonsterId, [1, 2]);

        Assert.Equal(1, member.QuestKillCounter);
        var payload = ZoneTestKit.DrainOutbound(memberPipe).AsSpan(1);
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(payload));
    }

    [Fact]
    public void KillCaptainArchetype_HidingPartyMember_IsNotCredited()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        EnterCharacter(zone, 1, QuestProgress.None);
        var (_, member, memberPipe) = EnterCharacter(zone, 2, new QuestProgress(3, 1, 5, MonsterId, 0));
        member.VisibleState = 0;
        ZoneTestKit.DrainOutbound(memberPipe);

        zone.ApplyQuestKillProgress(1, MonsterId, [1, 2]);

        Assert.Equal(0, member.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(memberPipe));
    }

    [Fact]
    public void KillCaptainArchetype_DeadPartyMember_IsNotCredited()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        EnterCharacter(zone, 1, QuestProgress.None);
        var (_, member, memberPipe) = EnterCharacter(zone, 2, new QuestProgress(3, 1, 5, MonsterId, 0));
        member.IsDead = true;
        ZoneTestKit.DrainOutbound(memberPipe);

        zone.ApplyQuestKillProgress(1, MonsterId, [1, 2]);

        Assert.Equal(0, member.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(memberPipe));
    }

    [Fact]
    public void KillCaptainArchetype_MidZoneTransferPartyMember_IsNotCredited()
    {
        var zone = ZoneTestKit.CreateZone(MapId);
        EnterCharacter(zone, 1, QuestProgress.None);
        var (_, member, memberPipe) = EnterCharacter(zone, 2, new QuestProgress(3, 1, 5, MonsterId, 0));
        member.IsMovingZone = true;
        ZoneTestKit.DrainOutbound(memberPipe);

        zone.ApplyQuestKillProgress(1, MonsterId, [1, 2]);

        Assert.Equal(0, member.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(memberPipe));
    }

    [Fact]
    public void KillMonstersArchetype_KillerSolo_BelowThreshold_AdvancesCounter_AndPushesMarkerSix()
    {
        var worldData = WorldDataWithKillMonstersQuest(1, solution2: 5);
        var zone = ZoneTestKit.CreateZone(MapId, worldData: worldData);
        var (_, killer, pipe) = EnterCharacter(zone, 1, new QuestProgress(3, 1, 1, MonsterId, 0));
        ZoneTestKit.DrainOutbound(pipe);

        zone.ApplyQuestKillProgress(1, MonsterId);

        Assert.Equal(1, killer.QuestKillCounter);
        var payload = ZoneTestKit.DrainOutbound(pipe).AsSpan(1);
        Assert.Equal(6, BinaryPrimitives.ReadInt32LittleEndian(payload));
    }

    [Fact]
    public void KillMonstersArchetype_VisiblePartyMember_IsCredited_AndPushesMarkerEight()
    {
        var worldData = WorldDataWithKillMonstersQuest(1, solution2: 5);
        var zone = ZoneTestKit.CreateZone(MapId, worldData: worldData);
        var (_, _, killerPipe) = EnterCharacter(zone, 1, QuestProgress.None);
        var (_, member, memberPipe) = EnterCharacter(zone, 2, new QuestProgress(3, 1, 1, MonsterId, 0));
        ZoneTestKit.DrainOutbound(killerPipe);
        ZoneTestKit.DrainOutbound(memberPipe);

        zone.ApplyQuestKillProgress(1, MonsterId, [1, 2]);

        Assert.Equal(1, member.QuestKillCounter);
        var payload = ZoneTestKit.DrainOutbound(memberPipe).AsSpan(1);
        Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(payload));
    }

    [Fact]
    public void KillMonstersArchetype_HidingPartyMember_IsNotCredited()
    {
        var worldData = WorldDataWithKillMonstersQuest(1, solution2: 5);
        var zone = ZoneTestKit.CreateZone(MapId, worldData: worldData);
        EnterCharacter(zone, 1, QuestProgress.None);
        var (_, member, memberPipe) = EnterCharacter(zone, 2, new QuestProgress(3, 1, 1, MonsterId, 0));
        member.VisibleState = 0;
        ZoneTestKit.DrainOutbound(memberPipe);

        zone.ApplyQuestKillProgress(1, MonsterId, [1, 2]);

        Assert.Equal(0, member.QuestKillCounter);
        Assert.Empty(ZoneTestKit.DrainOutbound(memberPipe));
    }
}
