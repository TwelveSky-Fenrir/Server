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
    private readonly FrozenDictionary<byte, short> _maxStepByCategory;

    public QuestCatalog(WorldDataCache worldData)
    {
        var quests = worldData.QuestsById.Values;
        _byCategoryStep = quests.ToFrozenDictionary(q => (q.Quest.Category, q.Quest.Step));
        _maxStepByCategory = quests
            .GroupBy(q => q.Quest.Category)
            .ToFrozenDictionary(g => g.Key, g => g.Max(q => q.Quest.Step));
    }

    /// <summary>Null (not found) is expected -- e.g. the tribe's chain ends at this step.</summary>
    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        return _byCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }

    /// <summary>
    ///     The terminal ("already finished") quest step for <paramref name="tribe" />'s own quest chain -- used
    ///     by the fourth-tribe (Fujin) return behavior to mark a restored tribe's quest chain as already
    ///     complete rather than reset to the beginning (<c>ReturnMaxStep</c>,
    ///     Server/ts25zone/GameSystem/GameSystem_06_Quest.cpp:40-56 per the translating behavior contract, not
    ///     independently re-verified). Returns 0 if this tribe's category has no quest rows loaded at all.
    /// </summary>
    public short MaxStep(byte tribe)
    {
        return _maxStepByCategory.TryGetValue((byte)(tribe + 1), out var step) ? step : (short)0;
    }
}
