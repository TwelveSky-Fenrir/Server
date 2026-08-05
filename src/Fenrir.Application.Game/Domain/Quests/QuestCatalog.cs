using System.Collections.Frozen;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Quests;

public sealed class QuestCatalog
{
    private QuestCatalogSnapshot _snapshot;

    public QuestCatalog(WorldDataCache worldData)
    {
        _snapshot = BuildSnapshot(worldData);
    }

    public void Reload(WorldDataCache worldData)
    {
        ArgumentNullException.ThrowIfNull(worldData);
        Volatile.Write(ref _snapshot, BuildSnapshot(worldData));
    }

    public QuestDefinition? TryGet(byte tribe, int step)
    {
        if (step is < 0 or > short.MaxValue)
            return null;

        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.ByCategoryStep.TryGetValue(((byte)(tribe + 1), (short)step), out var quest) ? quest : null;
    }

    public short TerminalStep(byte tribe)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.TerminalStepByCategory.TryGetValue((byte)(tribe + 1), out var step) ? step : (short)0;
    }

    public short MaxStep(byte tribe)
    {
        return TerminalStep(tribe);
    }

    private static QuestCatalogSnapshot BuildSnapshot(WorldDataCache worldData)
    {
        worldData = worldData.Capture();
        var quests = worldData.QuestsById.Values;
        var byCategoryStep = quests.ToFrozenDictionary(q => (q.Quest.Category, q.Quest.Step));
        var terminalSteps = new Dictionary<byte, short>();
        foreach (var quest in quests.OrderBy(static q => q.Quest.QuestId))
            if (quest.Quest.NextIndex is not > 0 && !terminalSteps.ContainsKey(quest.Quest.Category))
                terminalSteps.Add(quest.Quest.Category, quest.Quest.Step);

        return new QuestCatalogSnapshot(byCategoryStep, terminalSteps.ToFrozenDictionary());
    }

    private sealed record QuestCatalogSnapshot(
        FrozenDictionary<(byte Category, short Step), QuestDefinition> ByCategoryStep,
        FrozenDictionary<byte, short> TerminalStepByCategory);
}
