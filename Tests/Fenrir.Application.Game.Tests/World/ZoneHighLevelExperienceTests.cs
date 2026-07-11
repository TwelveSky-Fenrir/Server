using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers <see cref="Zone.ApplyHighLevelExperienceGain" /> -- the B17-rebirth-hook orchestration method
///     that resolves a post-general-level-cap award via <see cref="HighLevelExperienceResolver" /> and applies
///     it. Most tests here call the method directly to isolate its own branches; see
///     <see cref="ZoneHighLevelExperienceWiringTests" /> for coverage of the actual fork inside
///     <c>Zone.Combat.cs</c>'s <c>ApplyCharacterExperienceGain</c> that routes <see cref="Zone.GrantMonsterKillExperience" />/
///     PvP-kill awards here once a recipient is at the general-level cap. <c>HighLevelExperienceInputFactoryTests</c>/
///     <c>HighLevelExperienceOutcomeApplierTests</c> (under <c>Tests.Progression</c>) cover the two adapter
///     halves in isolation; this covers the zone-scoped tail (stat recompute, heal, dirty-mark, broadcast).
/// </summary>
public class ZoneHighLevelExperienceTests
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

    private static (Zone Zone, DirtyTracker<int> DirtyTracker, FakeDuplexPipe Pipe, PlayerRuntimeState Target)
        SetUpCharacter(short level, long experience, short level2, int exp2, int life = 1, int maxLife = 500)
    {
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            worldData: ZoneTestKit.EmptyWorldData(levelsByLevel: LevelsWithCapRow()));

        var (session, pipe) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Ascended", 1, 0, 2, 3, level,
            1, 0f, 0f, 0f, 0f,
            life, maxLife, 1, 1, 1,
            Experience: experience, Level2: level2, Exp2: exp2);
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        // Isolate what THIS test's own call sends/marks dirty from whatever Enter/Tick already did.
        dirtyTracker.DrainAll();
        ZoneTestKit.DrainOutbound(pipe);

        zone.TryGetPlayer(1, out var target);
        return (zone, dirtyTracker, pipe, target!);
    }

    [Fact]
    public void BelowGeneralCap_IsSilentNoOp_NoStateChangeNoDirtyNoBroadcast()
    {
        var (zone, dirtyTracker, pipe, target) = SetUpCharacter(144, 500, 0, 0);

        zone.ApplyHighLevelExperienceGain(target, 1_000_000);

        Assert.Equal(500, target.Experience);
        Assert.Equal(0, target.Level2);
        Assert.Empty(dirtyTracker.DrainAll());
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void StageA_PoolFill_UpdatesExperienceGrantsStatPointsMarksDirty_ButSendsNoBroadcast()
    {
        // Band [1e9, 2e9]; filling 200M of it (1e9 -> 1.2e9) crosses 20% -> 20 stat points.
        var (zone, dirtyTracker, pipe, target) = SetUpCharacter(145, 1_000_000_000, 0, 0);
        var priorStatPoints = target.StatPoints;

        zone.ApplyHighLevelExperienceGain(target, 200_000_000);

        Assert.Equal(1_200_000_000, target.Experience);
        Assert.Equal(priorStatPoints + 20, target.StatPoints);
        Assert.Equal(0, target.Level2); // Stage A never touches the rebirth-tier ladder

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(1, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);

        // Open question (see Zone.HighLevelExperience.cs's own remarks): the live client-sync sub-code for
        // Stage A is unresolved, so nothing is pushed yet -- state is still durable via the dirty-mark above.
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void StageB_ZeroToOneLevelUp_GrantsSkillPointsResetsPoolAndGrantsZone101Bonus()
    {
        var (zone, _, _, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 0, exp2: 0);
        var priorZone101 = target.Zone101Time;
        var priorSkillPoints = target.SkillPoints;

        zone.ApplyHighLevelExperienceGain(target, 5000);

        Assert.Equal(1, target.Level2);
        Assert.Equal(0, target.Exp2);
        Assert.Equal(priorSkillPoints + HighLevelExperienceResolver.RebirthTierLevelUpSkillPoints,
            target.SkillPoints);
        Assert.Equal(priorZone101 + HighLevelExperienceResolver.Zone101TimeGrantOnFirstRebirthTier,
            target.Zone101Time);
    }

    [Fact]
    public void StageB_LevelUp_RecomputesMaxStatsAndFullHealsWhenAlive()
    {
        var (zone, _, _, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 0, exp2: 0, life: 1, maxLife: 500);

        zone.ApplyHighLevelExperienceGain(target, 5000);

        // Level-145 row's own Life/Mana factors (400/200), no Vitality/equipment -> MaxLife/MaxMana move to
        // those exact values, and the (alive) character is healed to them, not left at the pre-levelup value.
        Assert.Equal(400, target.MaxLife);
        Assert.Equal(200, target.MaxMana);
        Assert.Equal(400, target.Life);
        Assert.Equal(200, target.Mana);
        Assert.Equal(400, target.Stats?.MaxLife);
    }

    [Fact]
    public void StageB_LevelUp_DeadCharacter_RefreshesMaxButDoesNotHeal()
    {
        var (zone, _, _, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 0, exp2: 0, life: 0, maxLife: 500);

        zone.ApplyHighLevelExperienceGain(target, 5000);

        Assert.Equal(400, target.MaxLife); // cache-write is unconditional
        Assert.Equal(0, target.Life); // no heal while already dead
    }

    [Fact]
    public void StageB_LevelUp_MarksProgressionAndVitalsDirtyAndSendsABroadcast()
    {
        var (zone, dirtyTracker, pipe, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 0, exp2: 0);

        zone.ApplyHighLevelExperienceGain(target, 5000);

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(1, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);
        Assert.Equal(DirtyFlags.Vitals, flags & DirtyFlags.Vitals);

        // Discriminator-11 state-flag broadcast plus the one-shot zone-101 bonus push -- both confirmed
        // sub-codes (see Zone.HighLevelExperience.cs's own remarks); asserting non-empty rather than decoding
        // the exact frame bytes, matching this suite's existing ZoneLevelUpTests convention.
        Assert.NotEmpty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void StageB_LevelUpFromMidTier_NoZone101TimeBonus()
    {
        var threshold1 = RebirthProgression.HighLevelExpTable[0];
        var (zone, _, _, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 1, exp2: threshold1);
        var priorZone101 = target.Zone101Time;

        zone.ApplyHighLevelExperienceGain(target, 50);

        Assert.Equal(2, target.Level2);
        Assert.Equal(priorZone101, target.Zone101Time); // unchanged -- bonus is 0-to-1 only
    }

    [Fact]
    public void StageB_Accrual_UpdatesExp2GrantsStatPointsMarksDirty_ButSendsNoBroadcast()
    {
        var threshold1 = RebirthProgression.HighLevelExpTable[0];
        var (zone, dirtyTracker, pipe, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 1, exp2: 0);
        var priorStatPoints = target.StatPoints;
        var gain = 100_000_000;

        zone.ApplyHighLevelExperienceGain(target, gain);

        Assert.Equal(gain, target.Exp2);
        Assert.Equal(priorStatPoints + (int)((long)gain * 100 / threshold1), target.StatPoints);
        Assert.Equal(1, target.Level2); // accrual never moves the tier itself

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(1, out var flags));
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);

        Assert.Empty(ZoneTestKit.DrainOutbound(pipe)); // same unresolved-sub-code gap as Stage A
    }

    [Fact]
    public void StageB_Level12FullyMaxed_IsSilentNoOp()
    {
        var (zone, dirtyTracker, pipe, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 12, exp2: HighLevelExperienceResolver.RebirthTierExperienceCeiling);

        zone.ApplyHighLevelExperienceGain(target, 1_000_000);

        Assert.Equal(HighLevelExperienceResolver.RebirthTierExperienceCeiling, target.Exp2);
        Assert.Empty(dirtyTracker.DrainAll());
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }

    [Fact]
    public void StageB_AntiCheatFlagged_IsSilentNoOp()
    {
        var (zone, dirtyTracker, pipe, target) = SetUpCharacter(145, HighLevelExperienceResolver.MaxMainExperience,
            level2: 3, exp2: 100);

        zone.ApplyHighLevelExperienceGain(target, 5_000_000, antiCheatExperienceFlagged: true);

        Assert.Equal(100, target.Exp2);
        Assert.Empty(dirtyTracker.DrainAll());
        Assert.Empty(ZoneTestKit.DrainOutbound(pipe));
    }
}
