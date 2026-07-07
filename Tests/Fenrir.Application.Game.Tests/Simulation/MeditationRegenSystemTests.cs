using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="MeditationRegenSystem" />: passive HP/MP regen only while <c>aAction.aSort == 31</c>
///     (sitting), driven by the sit-skill riding on the same action.
/// </summary>
public class MeditationRegenSystemTests
{
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
    public void Sitting_RegeneratesHpAndMpOnceEveryTwoLegacyTicks()
    {
        // Gate is SimulationClock.MeditationRegenLegacyTicks (2 legacy ticks, ~1 s, the same shared
        // mTickCountFor01Second == 2 gate StunCountdownSystem consumes) -- a single legacy tick must NOT
        // regen anything; the same amount must apply only once the 2nd legacy tick's accumulator crosses
        // the gate. This is the critical-severity 2x-rate bug's own regression coverage.
        var (zone, state) = SetUp(84, 32); // MaxLife=840 -> 10/period, MaxMana=320 -> 10/period
        var startLife = state.Life;
        var startMana = state.Mana;

        // isResumeAction: true -- Sort 31 is legal only via CZ_UPDATE_AVATAR_ACTION (op16)'s own switch
        // (AvatarActionResumeWhitelist); op15's CharacterMotionWhitelist has no row for it at all in any
        // shipped build (PlayerRuntimeState.ActionSort's own remarks).
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(SimulationClock.LegacyTick); // 1st legacy tick: gate has not fired yet.

        Assert.Equal(startLife, state.Life);
        Assert.Equal(startMana, state.Mana);

        zone.Tick(SimulationClock.LegacyTick); // 2nd legacy tick: gate fires exactly once.

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
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));

        zone.Tick(SimulationClock.LegacyTick); // 1st legacy tick: gate has not fired yet.
        zone.Tick(SimulationClock.LegacyTick); // 2nd legacy tick: gate fires, clamped to the cap.

        Assert.Equal(state.MaxLife, state.Life);
        Assert.Equal(state.MaxMana, state.Mana);
    }

    [Fact]
    public void Regen_BurstOfMultipleLegacyTicks_CatchesUpWholePeriodsAndKeepsRemainder()
    {
        // MaxLife=840, divisor=255 (byte max) -> perPeriod = (int)(840/255) = 3.
        var (zone, state) = SetUp(255, 255);
        var startLife = state.Life;
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5), true));
        zone.Tick(TimeSpan.FromMilliseconds(50)); // apply the Move first (sets ActionSort)

        // 5 whole legacy ticks in one burst (host stall) -> 2 complete ~1s periods (2 ticks each) x
        // perPeriod(3) = 6, with 1 leftover legacy tick banked toward the next firing -- the same
        // full-catch-up-by-whole-periods translation StunCountdownSystem already uses for this identical
        // gate, not "3 legacy ticks worth" the way the pre-fix code scaled per legacy tick.
        zone.Tick(TimeSpan.FromMilliseconds(2500));

        Assert.Equal(startLife + 6, state.Life);
        Assert.Equal(1, state.MeditationRegenAccumulatorTicks);
    }
}
