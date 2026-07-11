using System.Collections.Frozen;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Progression;

public static class HighLevelExperienceInputFactory
{
    public static HighLevelExperienceInput Build(PlayerRuntimeState target, int awardedExperience,
        bool antiCheatExperienceFlagged, FrozenDictionary<short, LevelRowDto> levelsByLevel)
    {
        var mainExperienceFloor = levelsByLevel.TryGetValue(LevelProgressionCalculator.MaxLevel, out var maxLevelRow)
            ? maxLevelRow.ExpRangeMin
            : 0;

        return new HighLevelExperienceInput(
            target.Level,
            target.Experience,
            mainExperienceFloor,
            target.Level2,
            target.Exp2,
            awardedExperience,
            antiCheatExperienceFlagged);
    }
}
