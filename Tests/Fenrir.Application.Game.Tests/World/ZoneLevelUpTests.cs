using System.Collections.Frozen;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Application.Game.World;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     Covers the level-up side effects <see cref="Zone.GrantMonsterKillExperience" /> applies once a kill's XP
///     gain crosses a level threshold: new <see cref="PlayerRuntimeState.Level" />, accumulated
///     StatPoints/SkillPoints, a recomputed <see cref="PlayerRuntimeState.Stats" />/MaxLife/MaxMana, and a full
///     heal (<c>S07_MyGame03.cpp:228-296</c>). <see cref="LevelProgressionCalculatorTests" /> covers the pure
///     level-resolution math in isolation; this covers the wiring into live character state.
/// </summary>
public class ZoneLevelUpTests
{
    private static FrozenDictionary<short, LevelRowDto> TestLevels()
    {
        var dict = new Dictionary<short, LevelRowDto>
        {
            // Level 1's own floor row -- ReturnLevel's below-floor guard reads this unconditionally.
            [1] = new(1, 0, 99, 0, 0, 0, 0, 0, 0, 0, 0),
            [10] = new(10, 900, 999, 0, 0, 0, 0, 0, 0, 0, 0),
            // 7 skill points (RangeInfo3) and the Life/Mana factors the post-level-up MaxLife/MaxMana read.
            [11] = new(11, 1000, 1099, 7, 0, 0, 0, 0, 0, 500, 300)
        };
        return dict.ToFrozenDictionary();
    }

    private static (Zone Zone, DirtyTracker<int> DirtyTracker, int CharacterId) SetUpKillerAtLevelTenNearLevelUp()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            worldData: ZoneTestKit.EmptyWorldData(levelsByLevel: TestLevels()));

        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Killer", 1, 0, 2, 3, 10,
            1, 0f, 0f, 0f, 0f,
            1, 1, 1, 1, 1,
            Experience: 990, StatPoints: 100);
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        return (zone, dirtyTracker, 10);
    }

    [Fact]
    public void GrantMonsterKillExperience_CrossesLevelThreshold_AppliesNewLevelAndGrantsPoints()
    {
        var (zone, _, killerId) = SetUpKillerAtLevelTenNearLevelUp();

        // Same level, no favorable/unfavorable gap: raw gain = 90, /3 (below LV_M1) = 30 -> 990+30=1020 (level 11).
        zone.GrantMonsterKillExperience(killerId, 10, 90);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(1020, killer!.Experience);
        Assert.Equal(11, killer.Level);
        // Prior level 10 (< 99) -> +5 stat points; level 11's RangeInfo3 is 7 skill points.
        // SkillPoints starts at 0 -- PlayerEnterData/HandleEnter don't load an initial value for it yet
        // (a separate, pre-existing gap; the +7 grant itself is what's under test here).
        Assert.Equal(105, killer.StatPoints);
        Assert.Equal(7, killer.SkillPoints);
    }

    [Fact]
    public void GrantMonsterKillExperience_CrossesLevelThreshold_RecomputesMaxStatsAndFullHeals()
    {
        var (zone, _, killerId) = SetUpKillerAtLevelTenNearLevelUp();

        zone.GrantMonsterKillExperience(killerId, 10, 90);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        // No Vitality/Ki/equipment -> MaxLife/MaxMana are exactly level 11's own Life/Mana factors.
        Assert.Equal(500, killer!.MaxLife);
        Assert.Equal(300, killer.MaxMana);
        Assert.Equal(500, killer.Stats?.MaxLife);
        Assert.Equal(300, killer.Stats?.MaxMana);
        // Was alive (Life=1) before the level-up -> healed to the new max, not left at the pre-levelup value.
        Assert.Equal(500, killer.Life);
        Assert.Equal(300, killer.Mana);
    }

    [Fact]
    public void GrantMonsterKillExperience_CrossesLevelThreshold_MarksProgressionAndVitalsDirty()
    {
        var (zone, dirtyTracker, killerId) = SetUpKillerAtLevelTenNearLevelUp();

        zone.GrantMonsterKillExperience(killerId, 10, 90);

        var drained = dirtyTracker.DrainAll();
        Assert.True(drained.TryGetValue(killerId, out var flags));
        // Entering the zone itself already marks Position dirty -- assert the level-up's own two flags are
        // both present rather than requiring an exact match against whatever else the tick already merged in.
        Assert.Equal(DirtyFlags.Progression, flags & DirtyFlags.Progression);
        Assert.Equal(DirtyFlags.Vitals, flags & DirtyFlags.Vitals);
    }

    [Fact]
    public void GrantMonsterKillExperience_DeadKiller_DoesNotOverwriteZeroLifeOnLevelUp()
    {
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker,
            worldData: ZoneTestKit.EmptyWorldData(levelsByLevel: TestLevels()));

        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Killer", 1, 0, 2, 3, 10,
            1, 0f, 0f, 0f, 0f,
            // Already dead (Life=0) when the kill's XP lands -- the source's own aLifeValue>0 heal guard.
            0, 1, 1, 1, 1,
            Experience: 990, StatPoints: 100);
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.GrantMonsterKillExperience(10, 10, 90);

        zone.TryGetPlayer(10, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(11, killer!.Level);
        // MaxLife/MaxMana still refreshed (SetBasicAbilityFromEquip's own cache-write is unconditional)...
        Assert.Equal(500, killer.MaxLife);
        // ...but Life itself stays at 0 -- no heal while already dead.
        Assert.Equal(0, killer.Life);
    }
}
