using System.Collections.Frozen;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Quests;

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

    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        return _byCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }

    public short MaxStep(byte tribe)
    {
        return _maxStepByCategory.TryGetValue((byte)(tribe + 1), out var step) ? step : (short)0;
    }
}
