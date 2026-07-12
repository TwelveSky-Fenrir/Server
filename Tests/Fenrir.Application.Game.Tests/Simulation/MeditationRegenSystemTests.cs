using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Simulation;

public class MeditationRegenSystemTests
{
    private const int CharacterHpStatSort = 10;

    private const int CharacterMpStatSort = 11;

    private static byte[] ExpectedFrames(params ReadOnlySpan<AvatarStatUpdateResponse> packets)
    {
        var frameSize = FrameWriter.FrameSizeOf<AvatarStatUpdateResponse>();
        var buffer = new byte[frameSize * packets.Length];
        for (var i = 0; i < packets.Length; i++)
            FrameWriter.WriteFrame(in packets[i], buffer.AsSpan(i * frameSize, frameSize));
        return buffer;
    }

    private static SkillDefinition SitSkill(byte maxUpgradePoint, byte lifeDivisor, byte manaDivisor)
    {
        var row = new SkillRowDto(7, "Sit", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        var grade0 = new SkillGradeRowDto(7, 0, 0, lifeDivisor, manaDivisor, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0);
        var grade1 = new SkillGradeRowDto(7, 1, 0, lifeDivisor, manaDivisor, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0);
        return new SkillDefinition(row, ImmutableArray<SkillDescriptionRowDto>.Empty, [grade0, grade1]);
    }

    private static ActionInfo SitAction(int skillNumber, int gradeNum1)
    {
        return new ActionInfo
        {
            Type = 0, Sort = 31, Frame = 0,
            Location = [100, 0, 100], TargetLocation = [100, 0, 100],
            Front = 0, TargetFront = 0,
            PetLocation = new float[3], PetTargetLocation = new float[3], PetFront = 0, PetSort = 0,
            TargetObjectSort = 0, TargetObjectIndex = 0, TargetObjectUniqueNumber = 0,
            SkillNumber = skillNumber, SkillGradeNum1 = gradeNum1, SkillGradeNum2 = 0, SkillValue = 0
        };
    }

    private static (Zone Zone, PlayerRuntimeState State, FakeDuplexPipe Pipe) SetUp(byte lifeDivisor,
        byte manaDivisor)
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [7] = SitSkill(10, lifeDivisor, manaDivisor) }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            simulationSystems: [new AvatarOneSecondGateSystem(), new MeditationRegenSystem(worldData, dirtyTracker)],
            worldData: worldData);

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!, pipe);
    }

    [Fact]
    public void Sitting_RegeneratesHpAndMpOnceEveryTwoLegacyTicks()
    {
        var (zone, state, pipe) = SetUp(84, 32);
        var startLife = state.Life;
        var startMana = state.Mana;

        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(startLife, state.Life);
        Assert.Equal(startMana, state.Mana);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(startLife + 10, state.Life);
        Assert.Equal(startMana + 10, state.Mana);
    }

    [Fact]
    public void Sitting_RegenFiring_PushesAvatarStatUpdateResponseForBothStats_ToTheSittingPlayerOnly()
    {
        var (zone, state, pipe) = SetUp(84, 32);
        var startLife = state.Life;
        var startMana = state.Mana;

        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(
            new AvatarStatUpdateResponse { Sort = CharacterHpStatSort, Value = startLife + 10, Value2 = 0 },
            new AvatarStatUpdateResponse { Sort = CharacterMpStatSort, Value = startMana + 10, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public void Sitting_StatAlreadyAtMax_SendsNoPacketForThatStat_ButStillPushesTheOtherOne()
    {
        var (zone, state, pipe) = SetUp(84, 32);
        state.Life = state.MaxLife;
        var startMana = state.Mana;

        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(state.MaxLife, state.Life);
        Assert.Equal(startMana + 10, state.Mana);

        var sent = ZoneTestKit.DrainOutbound(pipe);
        var expected = ExpectedFrames(
            new AvatarStatUpdateResponse { Sort = CharacterMpStatSort, Value = startMana + 10, Value2 = 0 });
        Assert.Equal(expected, sent);
    }

    [Fact]
    public void Sitting_UnresolvedSkill_RegeneratesNothingAndSendsNoPacket()
    {
        var (zone, state, pipe) = SetUp(84, 32);
        var startLife = state.Life;
        var startMana = state.Mana;

        zone.Post(ZoneCommand.Move(10, SitAction(999, 5), true));
        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(startLife, state.Life);
        Assert.Equal(startMana, state.Mana);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void NotSitting_NeverRegenerates()
    {
        var (zone, state, pipe) = SetUp(84, 32);
        var startLife = state.Life;

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(startLife, state.Life);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Regen_NeverExceedsMaxLife()
    {
        var (zone, state, _) = SetUp(1, 1);
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));

        zone.Tick(SimulationClock.LegacyTick);
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(state.MaxLife, state.Life);
        Assert.Equal(state.MaxMana, state.Mana);
    }

    [Fact]
    public void MidZoneTransfer_NeverRegeneratesEvenWhileSitting()
    {
        var (zone, state, pipe) = SetUp(84, 32);
        var startLife = state.Life;
        var startMana = state.Mana;

        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(SimulationClock.LegacyTick);
        state.IsMovingZone = true;
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(startLife, state.Life);
        Assert.Equal(startMana, state.Mana);
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void Regen_BurstOfMultipleLegacyTicks_CatchesUpWholePeriodsAndKeepsRemainder()
    {
        var (zone, state, _) = SetUp(255, 255);
        var startLife = state.Life;
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.Tick(TimeSpan.FromMilliseconds(2500));

        Assert.Equal(startLife + 6, state.Life);
        Assert.Equal(4, state.LastOneSecondGateTick);
    }
}
