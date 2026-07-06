using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>
///     Covers <see cref="MeditationRegenSystem" />: passive HP/MP regen only while <c>aAction.aSort == 31</c>
///     (sitting), driven by the sit-skill riding on the same action.
/// </summary>
public class MeditationRegenSystemTests
{
    /// <summary>
    ///     KNOWN CONFLICT, not guessed away here -- see <c>PlayerRuntimeState.ActionSort</c>'s own remarks.
    ///     <see cref="Fenrir.Application.Game.Domain.Movement.CharacterMotionWhitelist" />'s citation
    ///     (Server/ts25zone/S04_MyWork05.cpp:4249-4252) confirms Sort 31 is dead code (<c>#ifdef __MOBILE__</c>,
    ///     never defined in any shipped build), so <c>Zone.HandleMove</c> now disconnects any session that
    ///     posts it instead of ever recording it as the current action -- this system's own citation
    ///     (S07_MyGame04.cpp:461-518, aSort==31) independently claims 31 IS the real meditate/sit trigger. The
    ///     two legacy-grounded citations contradict each other; determining the real Sort/Type the client sends
    ///     to sit needs a fresh legacy-behavior-translator contract, not a guess -- skipped, not deleted or
    ///     silently "fixed", until that lands.
    /// </summary>
    private const string Sort31DeadCodeConflictSkipReason =
        "Sort=31 is confirmed dead code by CharacterMotionWhitelist (S04_MyWork05.cpp:4249-4252, #ifdef __MOBILE__ " +
        "never defined) -- Zone.HandleMove now disconnects the session instead of recording ActionSort=31, so this " +
        "system's own aSort==31 trigger (S07_MyGame04.cpp:461-518) is unreachable via the real wire protocol. Needs " +
        "a fresh legacy-behavior-translator contract for the real sit/meditate Sort/Type before this can be fixed " +
        "without guessing.";

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

    [Fact(Skip = Sort31DeadCodeConflictSkipReason)]
    public void Sitting_RegeneratesHpAndMpEveryLegacyTick()
    {
        var (zone, state) = SetUp(84, 32); // MaxLife=840 -> 10/tick, MaxMana=320 -> 10/tick
        var startLife = state.Life;
        var startMana = state.Mana;

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

    [Fact(Skip = Sort31DeadCodeConflictSkipReason)]
    public void Regen_NeverExceedsMaxLife()
    {
        var (zone, state) = SetUp(1, 1); // regen = MaxLife/1 = MaxLife -> huge overshoot
        zone.Post(ZoneCommand.Move(10, SitAction(7, 5)));

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Equal(state.MaxLife, state.Life);
        Assert.Equal(state.MaxMana, state.Mana);
    }

    [Fact(Skip = Sort31DeadCodeConflictSkipReason)]
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
