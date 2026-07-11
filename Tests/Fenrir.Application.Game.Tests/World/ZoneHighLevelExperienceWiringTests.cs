using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Confirms the actual fork inside <c>Zone.Combat.cs</c>'s <c>ApplyCharacterExperienceGain</c> (the shared
///     level-up cascade every experience-award chokepoint funnels through) really routes an at-general-level-cap
///     recipient to <see cref="Zone.ApplyHighLevelExperienceGain" /> instead of the ordinary level-up branch --
///     exercised here through the public <see cref="Zone.GrantMonsterKillExperience" /> entry point rather than
///     calling <see cref="Zone.ApplyHighLevelExperienceGain" /> directly (that direct-call coverage already lives
///     in <see cref="ZoneHighLevelExperienceTests" />). <c>Zone.ApplyPvpKillExperience</c> and the party-bonus
///     loop share the exact same private cascade, so this one wiring proof covers all three chokepoints
///     without duplicating per-caller setup.
/// </summary>
public class ZoneHighLevelExperienceWiringTests
{
    private static FrozenDictionary<short, LevelRowDto> LevelsWithCapRow()
    {
        var dict = new Dictionary<short, LevelRowDto>
        {
            [1] = new(1, 0, 99, 0, 0, 0, 0, 0, 0, 0, 0),
            [145] = new(145, 1_000_000_000, 1_999_999_999, 0, 0, 0, 0, 0, 0, 400, 200)
        };
        return dict.ToFrozenDictionary();
    }

    private static (Zone Zone, DirtyTracker<int> DirtyTracker, FakeDuplexPipe Pipe, int CharacterId) SetUpKillerAtCap(
        short level2, int exp2, long experience)
    {
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            worldData: ZoneTestKit.EmptyWorldData(levelsByLevel: LevelsWithCapRow()));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Ascended", 1, 0, 2, 3, LevelProgressionCalculator.MaxLevel,
            1, 0f, 0f, 0f, 0f,
            1, 500, 1, 1, 1,
            Experience: experience, Level2: level2, Exp2: exp2);
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        dirtyTracker.DrainAll();
        ZoneTestKit.DrainOutbound(pipe);

        return (zone, dirtyTracker, pipe, 10);
    }

    [Fact]
    public void GrantMonsterKillExperience_KillerAtGeneralLevelCap_RoutesToHighLevelResolverInsteadOfOrdinaryLevelUp()
    {
        // Saturated general pool + rebirth-tier 0 -> Stage B, and level-0's own threshold is 0, so any positive
        // final gain triggers an immediate rebirth-tier level-up (Exp2 discarded, Level2 0 -> 1).
        var (zone, dirtyTracker, pipe, killerId) = SetUpKillerAtCap(
            level2: 0, exp2: 0, experience: HighLevelExperienceResolver.MaxMainExperience);

        zone.TryGetPlayer(killerId, out var before);
        var priorSkillPoints = before!.SkillPoints;
        var priorZone101 = before.Zone101Time;

        // fixedLevel(145) = 335 (ExperienceFormulas.ReturnFixedLevel post-cap table); a same-level monster (no
        // gap) with general experience 500 -> raw 500, /5 (>= LV_M1) rebirth divisor -> final gain 100.
        zone.GrantMonsterKillExperience(killerId, monsterLevel: 335, monsterGeneralExperience: 500);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);

        // The ordinary level-up path never ran: aLevel1 and the general-experience pool are untouched.
        Assert.Equal(LevelProgressionCalculator.MaxLevel, killer!.Level);
        Assert.Equal(HighLevelExperienceResolver.MaxMainExperience, killer.Experience);

        // The high-level resolver's own Stage B level-up DID run instead.
        Assert.Equal(1, killer.Level2);
        Assert.Equal(0, killer.Exp2);
        Assert.Equal(priorSkillPoints + HighLevelExperienceResolver.RebirthTierLevelUpSkillPoints,
            killer.SkillPoints);
        Assert.Equal(priorZone101 + HighLevelExperienceResolver.Zone101TimeGrantOnFirstRebirthTier,
            killer.Zone101Time);

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(killerId, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);
        Assert.Equal(DirtyFlags.Vitals, flags & DirtyFlags.Vitals);

        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void GrantMonsterKillExperience_KillerBelowGeneralLevelCap_StillUsesOrdinaryLevelUpPath()
    {
        // Sanity/regression companion: a below-cap recipient must still take the ordinary Zone.Combat.cs
        // level-up branch -- the fork must not swallow awards it doesn't own. Deliberately the fully-empty
        // world data (no level rows at all, same as ZonePartyExperienceTests/ZoneLevelUpTests' own convention)
        // so ReturnLevel never derives a spurious level change -- only the routing decision is under test here.
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "BelowCap", level: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.GrantMonsterKillExperience(10, monsterLevel: 50, monsterGeneralExperience: 1000);

        zone.TryGetPlayer(10, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(50, killer!.Level); // unchanged -- no level row data means ReturnLevel never crosses a level
        Assert.Equal(0, killer.Level2); // untouched -- the high-level resolver never ran
        Assert.True(killer.Experience > 0); // ordinary path did apply the gain
    }
}
