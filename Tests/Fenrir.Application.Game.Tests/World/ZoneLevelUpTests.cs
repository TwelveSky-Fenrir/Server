using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneLevelUpTests
{
    private static FrozenDictionary<short, LevelRowDto> TestLevels()
    {
        var dict = new Dictionary<short, LevelRowDto>
        {
            [1] = new(1, 0, 99, 0, 0, 0, 0, 0, 0, 0, 0),
            [10] = new(10, 900, 999, 0, 0, 0, 0, 0, 0, 0, 0),
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

        zone.GrantMonsterKillExperience(killerId, 10, 90);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(1020, killer!.Experience);
        Assert.Equal(11, killer.Level);
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
        Assert.Equal(500, killer!.MaxLife);
        Assert.Equal(300, killer.MaxMana);
        Assert.Equal(500, killer.Stats?.MaxLife);
        Assert.Equal(300, killer.Stats?.MaxMana);
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
            0, 1, 1, 1, 1,
            Experience: 990, StatPoints: 100);
        zone.Post(ZoneCommand.Enter(10, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.GrantMonsterKillExperience(10, 10, 90);

        zone.TryGetPlayer(10, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(11, killer!.Level);
        Assert.Equal(500, killer.MaxLife);
        Assert.Equal(0, killer.Life);
    }
}
