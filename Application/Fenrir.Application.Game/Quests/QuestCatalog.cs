using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Quests;

/// <summary>
///     Indexes <see cref="WorldDataCache.QuestsById" /> by (Category, Step) -- mirrors
///     <c>mQUEST.Search(tribe, step)</c>'s lookup shape (Category = tribe + 1). Built once at DI
///     construction from the already-loaded, immutable <see cref="WorldDataCache" />, same "cheap derived
///     index over a singleton" pattern as <see cref="Stats.SetBonusTables" />.
/// </summary>
public sealed class QuestCatalog
{
    private readonly FrozenDictionary<(byte Category, short Step), QuestDefinition> _byCategoryStep;

    public QuestCatalog(WorldDataCache worldData)
    {
        _byCategoryStep = worldData.QuestsById.Values
            .ToFrozenDictionary(q => (q.Quest.Category, q.Quest.Step));
    }

    /// <summary>
    ///     Resolves <c>mQUEST.Search(tribe, step)</c> -- null (not found) is a normal, expected outcome (e.g. the tribe's
    ///     chain ends at this step).
    /// </summary>
    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        return _byCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }
}
