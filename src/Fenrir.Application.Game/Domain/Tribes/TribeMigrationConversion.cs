using Fenrir.Application.Game.Domain.Quests;

namespace Fenrir.Application.Game.Domain.Tribes;

public static class TribeMigrationConversion
{
    public static TribeMigrationResult Resolve(byte currentTribe, byte previousTribe, QuestCatalog questCatalog)
    {
        if (currentTribe != TribeMigrationGate.TribeFour)
            return new TribeMigrationResult(TribeMigrationGate.TribeFour, QuestProgress.None);

        var terminalStep = questCatalog.TerminalStep(currentTribe);
        var restoredProgress = new QuestProgress(terminalStep, 0, 0, 0, 0);
        return new TribeMigrationResult(previousTribe, restoredProgress);
    }
}

public readonly record struct TribeMigrationResult(byte NewTribe, QuestProgress NewQuestProgress);
