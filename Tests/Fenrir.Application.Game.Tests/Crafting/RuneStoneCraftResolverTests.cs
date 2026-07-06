using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Crafting;

public class RuneStoneCraftResolverTests
{
    private const int RuneCoreItemId = 93514;

    private static RuneStoneCraftRequest Request(
        int sourceItemId = RuneStoneCraftCatalog.AddStatItemId,
        int destinationItemId = RuneCoreItemId,
        int destinationPackedStat = 0,
        int statSlotSelector = RuneStoneCraftCatalog.StatSlotSelectorStrength,
        int sourcePage = ContainerMatrix.InventoryPage0,
        int sourceSlot = 0,
        int destinationPage = ContainerMatrix.InventoryPage0,
        int destinationSlot = 1,
        bool secondInventoryPageAccessible = true)
    {
        return new RuneStoneCraftRequest(sourcePage, sourceSlot, sourceItemId, destinationPage, destinationSlot,
            destinationItemId, destinationPackedStat, statSlotSelector, secondInventoryPageAccessible);
    }

    // --- Disconnect (malformed input) cases -------------------------------------------------------------

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 64)]
    public void OutOfRangeSourcePageOrSlot_Disconnects(int page, int slot)
    {
        var result = RuneStoneCraftResolver.Resolve(Request(sourcePage: page, sourceSlot: slot),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 64)]
    public void OutOfRangeDestinationPageOrSlot_Disconnects(int page, int slot)
    {
        var result = RuneStoneCraftResolver.Resolve(Request(destinationPage: page, destinationSlot: slot),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void SecondInventoryPageInaccessible_SourceOnExtendedPage_Disconnects()
    {
        var result = RuneStoneCraftResolver.Resolve(
            Request(sourcePage: ContainerMatrix.InventoryPage1, secondInventoryPageAccessible: false),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void SecondInventoryPageInaccessible_DestinationOnExtendedPage_Disconnects()
    {
        var result = RuneStoneCraftResolver.Resolve(
            Request(destinationPage: ContainerMatrix.InventoryPage1, secondInventoryPageAccessible: false),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Fact]
    public void SecondInventoryPageAccessible_ExtendedPageUse_DoesNotDisconnect()
    {
        var result = RuneStoneCraftResolver.Resolve(
            Request(sourcePage: ContainerMatrix.InventoryPage1, secondInventoryPageAccessible: true),
            new ScriptedRandomSource(0));

        Assert.NotEqual(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(0)] // empty slot
    [InlineData(1234)] // not a rune-stone item
    [InlineData(93514)] // a destination id, not a source id
    public void SourceItemNotOneOfTheThreeWhitelistedIds_Disconnects(int sourceItemId)
    {
        var result = RuneStoneCraftResolver.Resolve(Request(sourceItemId), new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    [InlineData(150)]
    [InlineData(500)]
    public void RerollOneStat_InvalidStatSlotSelector_Disconnects(int selector)
    {
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollOneStatItemId, statSlotSelector: selector),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(0)] // empty slot
    [InlineData(1234)] // not a rune-core item
    [InlineData(92296)] // a source id, not a destination id
    public void DestinationItemNotOneOfTheFourRuneCoreIds_Disconnects(int destinationItemId)
    {
        var result = RuneStoneCraftResolver.Resolve(Request(destinationItemId: destinationItemId),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    [Theory]
    [InlineData(93514)]
    [InlineData(93515)]
    [InlineData(93516)]
    [InlineData(93517)]
    public void EveryRuneCoreId_IsAcceptedAsDestination(int destinationItemId)
    {
        var result = RuneStoneCraftResolver.Resolve(Request(destinationItemId: destinationItemId),
            new ScriptedRandomSource(0));

        Assert.NotEqual(RuneStoneCraftOutcome.Disconnect, result.Outcome);
    }

    // --- 92296: add one random stat to the first empty slot ---------------------------------------------

    [Fact]
    public void AddStat_AllFourSlotsAlreadyFilled_IsRefusedWithResultCode10()
    {
        var packed = RuneStoneStatCodec.Encode(1, 1, 1, 1);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.AddStatItemId, destinationPackedStat: packed),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Refused, result.Outcome);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeAllStatsAlreadyFilled, result.ResultCode);
        Assert.Equal(0, result.NewPackedStat);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AddStat_AllEmpty_FillsStrFirst()
    {
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.AddStatItemId, destinationPackedStat: 0),
            new ScriptedRandomSource(199)); // top tier => 30

        Assert.True(result.Succeeded);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeSuccess, result.ResultCode);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(30, str);
        Assert.Equal(0, dex);
        Assert.Equal(0, vit);
        Assert.Equal(0, intel);
        Assert.Equal(RuneStoneCraftCatalog.NoSpecificSlot, result.LogSlotIndicator);
    }

    [Fact]
    public void AddStat_StrFilled_FillsDexNext()
    {
        var packed = RuneStoneStatCodec.Encode(5, 0, 0, 0);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.AddStatItemId, destinationPackedStat: packed),
            new ScriptedRandomSource(199));

        Assert.True(result.Succeeded);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(5, str);
        Assert.Equal(30, dex);
        Assert.Equal(0, vit);
        Assert.Equal(0, intel);
    }

    [Fact]
    public void AddStat_StrDexFilled_FillsVitNext()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 0, 0);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.AddStatItemId, destinationPackedStat: packed),
            new ScriptedRandomSource(199));

        Assert.True(result.Succeeded);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(5, str);
        Assert.Equal(6, dex);
        Assert.Equal(30, vit);
        Assert.Equal(0, intel);
    }

    [Fact]
    public void AddStat_OnlyIntEmpty_FillsInt()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 7, 0);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.AddStatItemId, destinationPackedStat: packed),
            new ScriptedRandomSource(199));

        Assert.True(result.Succeeded);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(5, str);
        Assert.Equal(6, dex);
        Assert.Equal(7, vit);
        Assert.Equal(30, intel);
    }

    // --- 92297: reroll all four stats at once ------------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, 0, 1, 1)]
    [InlineData(1, 1, 0, 1)]
    [InlineData(1, 1, 1, 0)]
    public void RerollAll_AnySlotNotYetFilled_IsRefusedWithResultCode11(sbyte str, sbyte dex, sbyte vit, sbyte intel)
    {
        var packed = RuneStoneStatCodec.Encode(str, dex, vit, intel);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollAllStatsItemId, destinationPackedStat: packed),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Refused, result.Outcome);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeNotAllStatsFilled, result.ResultCode);
        Assert.Equal(0, result.NewPackedStat);
    }

    [Fact]
    public void RerollAll_AllFourFilled_OverwritesAllFour()
    {
        var packed = RuneStoneStatCodec.Encode(1, 2, 3, 4);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollAllStatsItemId, destinationPackedStat: packed),
            new ScriptedRandomSource(199));

        Assert.True(result.Succeeded);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeSuccess, result.ResultCode);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(30, str);
        Assert.Equal(30, dex);
        Assert.Equal(30, vit);
        Assert.Equal(30, intel);
        Assert.Equal(RuneStoneCraftCatalog.NoSpecificSlot, result.LogSlotIndicator);
    }

    // --- 92298: reroll one selected stat -------------------------------------------------------------------

    [Theory]
    [InlineData(RuneStoneCraftCatalog.StatSlotSelectorStrength, 1)]
    [InlineData(RuneStoneCraftCatalog.StatSlotSelectorDexterity, 2)]
    [InlineData(RuneStoneCraftCatalog.StatSlotSelectorVitality, 3)]
    [InlineData(RuneStoneCraftCatalog.StatSlotSelectorIntelligence, 4)]
    public void RerollOne_SelectedSlotEmpty_IsRefusedWithResultCode14(int selector, int expectedSlotIndicator)
    {
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollOneStatItemId, destinationPackedStat: 0,
                statSlotSelector: selector),
            new ScriptedRandomSource(0));

        Assert.Equal(RuneStoneCraftOutcome.Refused, result.Outcome);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeSelectedStatEmpty, result.ResultCode);
        Assert.Equal(0, result.NewPackedStat);
        Assert.Equal(expectedSlotIndicator, result.LogSlotIndicator);
    }

    [Fact]
    public void RerollOne_SelectedSlotNonZero_RerollsOnlyThatSlot()
    {
        var packed = RuneStoneStatCodec.Encode(5, 6, 7, 8);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollOneStatItemId, destinationPackedStat: packed,
                statSlotSelector: RuneStoneCraftCatalog.StatSlotSelectorDexterity),
            new ScriptedRandomSource(199));

        Assert.True(result.Succeeded);
        Assert.Equal(RuneStoneCraftCatalog.ResultCodeSelectedStatSuccess, result.ResultCode);
        var (str, dex, vit, intel) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(5, str);
        Assert.Equal(30, dex);
        Assert.Equal(7, vit);
        Assert.Equal(8, intel);
        Assert.Equal(2, result.LogSlotIndicator);
    }

    [Fact]
    public void RerollOne_SelectedSlotNegative_IsNotTreatedAsEmpty_UnlikeTheOtherTwoBranches()
    {
        // Deliberate legacy asymmetry: 92298's emptiness test is strictly "== 0", not "<= 0" like 92296/92297.
        var packed = RuneStoneStatCodec.Encode(-5, 0, 0, 0);
        var result = RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollOneStatItemId, destinationPackedStat: packed,
                statSlotSelector: RuneStoneCraftCatalog.StatSlotSelectorStrength),
            new ScriptedRandomSource(199));

        Assert.True(result.Succeeded);
        var (str, _, _, _) = RuneStoneStatCodec.Decode(result.NewPackedStat);
        Assert.Equal(30, str);
    }

    // --- Roll generation happens up front regardless of branch/selector ----------------------------------

    [Fact]
    public void FourRollsAreAlwaysDrawn_RegardlessOfWhichBranchRuns()
    {
        var random = new CountingRandomSource();
        RuneStoneCraftResolver.Resolve(
            Request(RuneStoneCraftCatalog.RerollOneStatItemId, destinationPackedStat: 0,
                statSlotSelector: RuneStoneCraftCatalog.StatSlotSelectorStrength),
            random);

        // Refused (selected slot empty) still draws all 4 rolls up front before the branch even inspects them.
        Assert.Equal(4, random.CallCount);
    }

    private sealed class CountingRandomSource : IRandomSource
    {
        public int CallCount { get; private set; }

        public int NextInt32(int exclusiveUpperBound)
        {
            CallCount++;
            return 0;
        }
    }
}
