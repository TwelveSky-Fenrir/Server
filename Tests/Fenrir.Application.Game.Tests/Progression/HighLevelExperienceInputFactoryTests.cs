using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Progression;

public class HighLevelExperienceInputFactoryTests
{
    private static FrozenDictionary<short, LevelRowDto> LevelsWithCapRow(int capExpRangeMin)
    {
        var dict = new Dictionary<short, LevelRowDto>
        {
            [1] = new(1, 0, 99, 0, 0, 0, 0, 0, 0, 0, 0),
            [145] = new(145, capExpRangeMin, capExpRangeMin + 999_999_999, 0, 0, 0, 0, 0, 0, 0, 0)
        };
        return dict.ToFrozenDictionary();
    }

    private static (Zone Zone, int CharacterId) SetUpCharacter(short level, long experience, short level2,
        int exp2)
    {
        var zone = ZoneTestKit.CreateZone(1);
        var (session, _) = ZoneTestKit.CreateSession(1);
        var enterData = new PlayerEnterData(
            session, "Ascended", 1, 0, 2, 3, level,
            1, 0f, 0f, 0f, 0f,
            1, 1, 1, 1, 1,
            Experience: experience, Level2: level2, Exp2: exp2);
        zone.Post(ZoneCommand.Enter(1, enterData));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        return (zone, 1);
    }

    [Fact]
    public void Build_CopiesLevelExperienceLevel2Exp2FromState()
    {
        var (zone, characterId) = SetUpCharacter(145, 1_500_000_000, 4, 300_000_000);
        zone.TryGetPlayer(characterId, out var target);

        var input = HighLevelExperienceInputFactory.Build(target!, 12345, false, LevelsWithCapRow(1_000_000_000));

        Assert.Equal(145, input.Level);
        Assert.Equal(1_500_000_000, input.MainExperience);
        Assert.Equal(4, input.Level2);
        Assert.Equal(300_000_000, input.Exp2);
        Assert.Equal(12345, input.AwardedExperience);
        Assert.False(input.AntiCheatExperienceFlagged);
    }

    [Fact]
    public void Build_ForwardsAntiCheatFlag()
    {
        var (zone, characterId) = SetUpCharacter(145, 0, 0, 0);
        zone.TryGetPlayer(characterId, out var target);

        var input = HighLevelExperienceInputFactory.Build(target!, 100, true, LevelsWithCapRow(1_000_000_000));

        Assert.True(input.AntiCheatExperienceFlagged);
    }

    [Fact]
    public void Build_CapRowPresent_UsesItsExpRangeMinAsMainExperienceFloor()
    {
        var (zone, characterId) = SetUpCharacter(145, 0, 0, 0);
        zone.TryGetPlayer(characterId, out var target);

        var input = HighLevelExperienceInputFactory.Build(target!, 100, false, LevelsWithCapRow(987_654_321));

        Assert.Equal(987_654_321, input.MainExperienceFloor);
    }

    [Fact]
    public void Build_CapRowAbsent_FallsBackToZeroFloor()
    {
        var (zone, characterId) = SetUpCharacter(145, 0, 0, 0);
        zone.TryGetPlayer(characterId, out var target);

        var emptyLevels = new Dictionary<short, LevelRowDto>().ToFrozenDictionary();
        var input = HighLevelExperienceInputFactory.Build(target!, 100, false, emptyLevels);

        Assert.Equal(0, input.MainExperienceFloor);
    }

    [Fact]
    public void Build_NeverMutatesTheSuppliedState()
    {
        var (zone, characterId) = SetUpCharacter(145, 1_000_000_000, 2, 50_000_000);
        zone.TryGetPlayer(characterId, out var target);

        HighLevelExperienceInputFactory.Build(target!, 999_999, true, LevelsWithCapRow(1_000_000_000));

        Assert.Equal(1_000_000_000, target!.Experience);
        Assert.Equal(2, target.Level2);
        Assert.Equal(50_000_000, target.Exp2);
    }
}
