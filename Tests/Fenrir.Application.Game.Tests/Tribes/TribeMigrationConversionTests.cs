using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeMigrationConversionTests
{
    private static QuestCatalog Catalog(params QuestRowDto[] quests)
    {
        var rows = WorldDataTestRows.MinimalRows() with { Quests = quests, QuestRewards = [], QuestSpeeches = [] };
        var (cache, _) = WorldDataCacheBuilder.Build(rows);
        return new QuestCatalog(cache);
    }

    [Fact]
    public void Resolve_Outbound_TribeBecomesThree_QuestStateResetsToIdle()
    {
        var catalog = Catalog();

        var result = TribeMigrationConversion.Resolve(0, 0, catalog);

        Assert.Equal((byte)3, result.NewTribe);
        Assert.Equal(QuestProgress.None, result.NewQuestProgress);
        Assert.True(result.NewQuestProgress.IsIdle);
    }

    [Fact]
    public void Resolve_Outbound_IgnoresPreviousTribeEntirely()
    {
        var catalog = Catalog();

        var fromTribeZero = TribeMigrationConversion.Resolve(0, 0, catalog);
        var fromTribeTwo = TribeMigrationConversion.Resolve(2, 1, catalog);

        Assert.Equal(fromTribeZero.NewTribe, fromTribeTwo.NewTribe);
        Assert.Equal(fromTribeZero.NewQuestProgress, fromTribeTwo.NewQuestProgress);
    }

    [Fact]
    public void Resolve_Return_TribeRestoredToPreviousTribe_QuestSlotZeroSetToTerminalStep()
    {
        // Tribe 1's own quest chain (Category 2) has two steps; the restored tribe's quest slot 0 must land
        // on the HIGHER one (already-complete), never reset to 0/the beginning.
        var stepOne = WorldDataTestRows.Quest(1) with { Category = 2, Step = 1 };
        var stepTwo = WorldDataTestRows.Quest(2) with { Category = 2, Step = 2 };
        var catalog = Catalog(stepOne, stepTwo);

        var result = TribeMigrationConversion.Resolve(3, 1, catalog);

        Assert.Equal((byte)1, result.NewTribe);
        Assert.Equal(2, result.NewQuestProgress.StepPermanent);
        Assert.Equal(0, result.NewQuestProgress.ActiveFlag);
        Assert.Equal(0, result.NewQuestProgress.QSort);
        Assert.Equal(0, result.NewQuestProgress.TargetPhase);
        Assert.Equal(0, result.NewQuestProgress.KillCounter);
        // Idle by the state machine's own definition (survives StepPermanent), but never touches money/items --
        // Resolve itself never mints a reward, only quest bookkeeping.
        Assert.True(result.NewQuestProgress.IsIdle);
    }

    [Fact]
    public void Resolve_Return_NoQuestRowsForRestoredTribe_TerminalStepIsZero()
    {
        var catalog = Catalog();

        var result = TribeMigrationConversion.Resolve(3, 2, catalog);

        Assert.Equal((byte)2, result.NewTribe);
        Assert.Equal(0, result.NewQuestProgress.StepPermanent);
    }
}
