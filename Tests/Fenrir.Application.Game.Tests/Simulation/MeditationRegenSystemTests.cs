using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="MeditationRegenSystem" /> (report 05 §7 point 3, <c>AVATAR_OBJECT::Update</c>,
///     S07_MyGame04.cpp:461-518): passive HP/MP regen ONLY while <c>aAction.aSort == 31</c> (sitting), driven
///     by the sit-skill riding on the same action.
/// </summary>
public class MeditationRegenSystemTests
{
    private static SkillDefinition SitSkill(byte maxUpgradePoint, byte lifeDivisor, byte manaDivisor)
    {
        var row = new SkillRowDto(7, "Sit", 0, 0, 0, 0, 0, 1, maxUpgradePoint, 1, 0);
        // 27 positional args: SkillId, GradeIndex, ManaUse, RecoverInfo1, RecoverInfo2, StunAttack, StunDefense,
        // FastRunSpeed, AttackInfo1-3, RunTime, ChargingDamageUp, AttackPowerUp, DefensePowerUp,
        // AttackSuccessUp, AttackBlockUp, ElementAttackUp, ElementDefenseUp, AttackSpeedUp, RunSpeedUp,
        // ShieldLifeUp, LuckUp, CriticalUp, ReturnSuccessUp, StunDefenseUp, DestroySuccessUp.
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

    private static (Zone Zone, PlayerRuntimeState State) SetUp(byte lifeDivisor, byte manaDivisor)
    {
        var skillsById = new Dictionary<int, SkillDefinition> { [7] = SitSkill(10, lifeDivisor, manaDivisor) }
            .ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(skillsById: skillsById);
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            simulationSystems: [new MeditationRegenSystem(worldData, dirtyTracker)], worldData: worldData);

        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(10, out var state));
        return (zone, state!);
    }

    [Fact]
    public void Sitting_RegeneratesHpAndMpEveryLegacyTick()
    {
        var (zone, state) = SetUp(84, 32); // MaxLife=840 -> 10/tick, MaxMana=320 -> 10/tick
        var startLife = state.Life; // 800
        var startMana = state.Mana; // 300

        zone.Post(ZoneCommand.Move(10, SitAction(7, 5)));
        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(startLife + 10, state.Life);
        Assert.Equal(startMana + 10, state.Mana);
    }

    [Fact]
    public void NotSitting_NeverRegenerates()
    {
        var (zone, state) = SetUp(84, 32);
        var startLife = state.Life;

        zone.Tick(SimulationClock.LegacyTick); // ActionSort stays 0 (idle) -- no Move posted.

        Assert.Equal(startLife, state.Life);
    }

    [Fact]
    public void Regen_NeverExceedsMaxLife()
    {
        var (zone, state) = SetUp(1, 1); // regen = MaxLife/1 = MaxLife -> huge overshoot
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5)));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(state.MaxLife, state.Life);
        Assert.Equal(state.MaxMana, state.Mana);
    }

    [Fact]
    public void Regen_BurstOfMultipleLegacyTicks_AppliesTheWholeAmount()
    {
        // MaxLife=840, divisor=255 (byte max) -> perTick = (int)(840/255) = 3.
        var (zone, state) = SetUp(255, 255);
        var startLife = state.Life;
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5)));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // apply the Move first (sets ActionSort)

        // 3 whole legacy ticks in one burst -> 3 x perTick(3) = 9.
        zone.Tick(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(startLife + 9, state.Life);
    }
}
