using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Quests;

/// <summary>
///     Indexes <see cref="WorldDataCache.QuestsById" /> by (Category, Step) -- <c>mQUEST.Search(tribe, step)</c>'s
///     own lookup shape (report 04 §5: "qCategory == tribu+1 &amp;&amp; qStep"), built ONCE at DI
///     construction time from the already-loaded, immutable <see cref="WorldDataCache" /> (same "cheap
///     derived index over a process-wide singleton" pattern <see cref="Stats.SetBonusTables" /> uses for
///     its own lookups). Category = tribe + 1 (world.Quests.Category is a <c>byte</c>, tribe is 0-3).
/// </summary>
public sealed class QuestCatalog
{
    private readonly FrozenDictionary<(byte Category, short Step), QuestDefinition> _byCategoryStep;

    public QuestCatalog(WorldDataCache worldData)
    {
        _byCategoryStep = worldData.QuestsById.Values
            .ToFrozenDictionary(q => (q.Quest.Category, q.Quest.Step));
    }

    /// <summary>Resolves <c>mQUEST.Search(tribe, step)</c> -- null (not found) is a normal, expected outcome (e.g. the tribe's chain ends at this step).</summary>
    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        return _byCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }
}
