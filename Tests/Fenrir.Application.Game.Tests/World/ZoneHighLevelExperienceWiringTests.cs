using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Tests.World;

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
        var (zone, dirtyTracker, pipe, killerId) = SetUpKillerAtCap(
            0, 0, HighLevelExperienceResolver.MaxMainExperience);

        zone.TryGetPlayer(killerId, out var before);
        var priorSkillPoints = before!.SkillPoints;
        var priorZone101 = before.Zone101Time;

        zone.GrantMonsterKillExperience(killerId, 335, 500);

        zone.TryGetPlayer(killerId, out var killer);
        Assert.NotNull(killer);

        Assert.Equal(LevelProgressionCalculator.MaxLevel, killer!.Level);
        Assert.Equal(HighLevelExperienceResolver.MaxMainExperience, killer.Experience);

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
        var dirtyTracker = new DirtyTracker<int>();
        var zone = ZoneTestKit.CreateZone(1, dirtyTracker: dirtyTracker);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "BelowCap", level: 50)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        zone.GrantMonsterKillExperience(10, 50, 1000);

        zone.TryGetPlayer(10, out var killer);
        Assert.NotNull(killer);
        Assert.Equal(50, killer!.Level);
        Assert.Equal(0, killer.Level2);
        Assert.True(killer.Experience > 0);
    }
}
