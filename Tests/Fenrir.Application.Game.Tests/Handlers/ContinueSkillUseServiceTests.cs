using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Handlers;

public class ContinueSkillUseServiceTests
{
    private static readonly int ActionFrame = FrameWriter.FrameSizeOf<AvatarActionResponse>();

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, PlayerRuntimeState State) Setup(Zone zone,
        int characterId, float posX = 10f, float posZ = 10f)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(characterId);
        session.MarkTicketConsumed(1, characterId);
        session.MarkRegistering();
        session.MarkInWorld();

        zone.Post(ZoneCommand.Enter(characterId,
            ZoneTestKit.EnterData(session, zone.MapId, posX: posX, posZ: posZ)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        session.CurrentZone = zone;

        zone.TryGetPlayer(characterId, out var state);
        return (session, pipe, state!);
    }

    [Fact]
    public void Sort1_ValidPreconditions_ReducesManaAndBroadcastsChannelingActionToSelfAndNeighbor()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var (_, neighborPipe, _) = Setup(zone, 20, 12f, 12f);
        ZoneTestKit.DrainOutbound(pipe);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 1;
        state.Mana = 100;
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AutoBuffActivationResolver.ResultKind.Activate, result.Kind);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(10, player!.Mana);
        Assert.Equal(41, player.ActionSort);

        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(pipe).Length);
        Assert.Equal(ActionFrame, ZoneTestKit.DrainOutbound(neighborPipe).Length);
    }

    [Fact]
    public void Sort1_AutoBuffTimeExpired_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var initialMana = state.Mana;
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AutoBuffActivationResolver.ResultKind.NoReply, result.Kind);
        Assert.True(zone.TryGetPlayer(10, out var player));
        Assert.Equal(initialMana, player!.Mana);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort1_NotIdle_NoReplyNoStateChange()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 30;
        state.Mana = 100;
        var service = new ContinueSkillUseService();

        service.Activate(zone, 10, state, 1);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(100, state.Mana);
        Assert.Equal(30, state.ActionSort);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort1_NegativeMana_DisconnectsPerLegacyQuirk()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, _, state) = Setup(zone, 10);
        state.AutoBuffTime = GameDate.Today();
        state.ActionSort = 1;
        state.Mana = -10;
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 1);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Disconnect, result.Kind);
    }

    [Fact]
    public void Sort2_IsASilentNoOp()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, pipe, state) = Setup(zone, 10);
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 2);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(AutoBuffActivationResolver.ResultKind.Tick, result.Kind);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Sort2_AppliesRegisteredSelfBuffUsingFreshlyRecomputedGrade()
    {
        var grade0 = new SkillGradeRowDto(82, 0, 30, 0, 0, 0, 0, 0, 0, 0, 0, 40, 0, 0, 0, 0, 0, 0, 0, 0, 0, 20, 0, 0,
            0, 0, 0);
        var skillDef = new SkillDefinition(
            new SkillRowDto(82, "Holy Shield", 0, 0, 0, 0, 0, 1, 10, 1, 0),
            ImmutableArray<SkillDescriptionRowDto>.Empty,
            [grade0, grade0 with { GradeIndex = 1 }]);
        var worldData = ZoneTestKit.EmptyWorldData(
            skillsById: new Dictionary<int, SkillDefinition> { [82] = skillDef }.ToFrozenDictionary());
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (_, _, state) = Setup(zone, 10);

        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(82, 10));
        state.AutoBuffSkill = ImmutableArray.Create(
            (82, 10), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0));

        var service = new ContinueSkillUseService();
        var result = service.Activate(zone, 10, state, 2);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Tick, result.Kind);
        Assert.Equal(168, state.Buffs.Buff[9 * 2]);
        Assert.Equal(40, state.Buffs.Buff[9 * 2 + 1]);
    }

    [Fact]
    public void Sort2_PartyBuffSlotWithoutFullConfirmedParty_IsSilentlySkipped()
    {
        var grade0 = new SkillGradeRowDto(76, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 40, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0);
        var skillDef = new SkillDefinition(
            new SkillRowDto(76, "Party Buff", 0, 0, 0, 0, 0, 1, 10, 1, 0),
            ImmutableArray<SkillDescriptionRowDto>.Empty,
            [grade0, grade0 with { GradeIndex = 1 }]);
        var worldData = ZoneTestKit.EmptyWorldData(
            skillsById: new Dictionary<int, SkillDefinition> { [76] = skillDef }.ToFrozenDictionary());
        var zone = ZoneTestKit.CreateZone(1, worldData: worldData);
        var (session, pipe, state) = Setup(zone, 10);

        state.LearnedSkills = state.LearnedSkills.SetItem(0, new LearnedSkill(76, 10));
        state.AutoBuffSkill = ImmutableArray.Create(
            (76, 10), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0));
        ZoneTestKit.DrainOutbound(pipe);

        var service = new ContinueSkillUseService();
        service.Activate(zone, 10, state, 2);
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, state.Buffs.Buff[2 * 2]);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void UnsupportedSort_Aborts()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (_, pipe, state) = Setup(zone, 10);
        var service = new ContinueSkillUseService();

        var result = service.Activate(zone, 10, state, 3);

        Assert.Equal(AutoBuffActivationResolver.ResultKind.Disconnect, result.Kind);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
