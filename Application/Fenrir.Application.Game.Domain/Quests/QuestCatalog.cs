using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Quests;

/// <summary>
///     Indexes WorldDataCache.QuestsById by (Category, Step) -- mirrors mQUEST.Search(tribe, step) (Category = tribe
///     + 1).
/// </summary>
public sealed class QuestCatalog
{
    private readonly FrozenDictionary<(byte Category, short Step), QuestDefinition> _byCategoryStep;

    public QuestCatalog(WorldDataCache worldData)
    {
        _byCategoryStep = worldData.QuestsById.Values
            .ToFrozenDictionary(q => (q.Quest.Category, q.Quest.Step));
    }

    /// <summary>Null (not found) is expected -- e.g. the tribe's chain ends at this step.</summary>
    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        return _byCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }
}
