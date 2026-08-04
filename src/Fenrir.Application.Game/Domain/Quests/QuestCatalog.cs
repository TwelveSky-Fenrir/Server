using System.Collections.Frozen;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Quests;

public sealed class QuestCatalog
{
    private readonly FrozenDictionary<(byte Category, short Step), QuestDefinition> _byCategoryStep;
    private readonly FrozenDictionary<byte, short> _terminalStepByCategory;

    public QuestCatalog(WorldDataCache worldData)
    {
        var quests = worldData.QuestsById.Values;
        _byCategoryStep = quests.ToFrozenDictionary(q => (q.Quest.Category, q.Quest.Step));
        var terminalSteps = new Dictionary<byte, short>();
        foreach (var quest in quests.OrderBy(static q => q.Quest.QuestId))
            if (quest.Quest.NextIndex is not > 0 && !terminalSteps.ContainsKey(quest.Quest.Category))
                terminalSteps.Add(quest.Quest.Category, quest.Quest.Step);

        _terminalStepByCategory = terminalSteps.ToFrozenDictionary();
    }

    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        return _byCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }

    public short TerminalStep(byte tribe)
    {
        return _terminalStepByCategory.TryGetValue((byte)(tribe + 1), out var step) ? step : (short)0;
    }

    public short MaxStep(byte tribe)
    {
        return TerminalStep(tribe);
    }
}
