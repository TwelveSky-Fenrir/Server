using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class EquipItemValidationGateTests
{
    private static EquipItemValidationGate.EquipCandidate Candidate(
        int itemId = 1000,
        int tribeRestriction = EquipItemValidationGate.AnyTribeSentinel,
        int equipPartTag = 2,
        int levelLimit = 0,
        int martialLevelLimit = 0,
        int checkSetItem = 0,
        int sort = 6)
    {
        return new EquipItemValidationGate.EquipCandidate(
            itemId, tribeRestriction, equipPartTag, levelLimit, martialLevelLimit, checkSetItem, sort);
    }

    [Fact]
    public void Evaluate_NullItem_ItemNotFound()
    {
        var outcome = EquipItemValidationGate.Evaluate(null, 0, 0, EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.ItemNotFound, outcome);
    }

    [Fact]
    public void Evaluate_AnyTribeSentinel_PassesForEveryTribe()
    {
        var candidate = Candidate(tribeRestriction: EquipItemValidationGate.AnyTribeSentinel);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 2, EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Fact]
    public void Evaluate_TribeRestrictionMatchesAfterOffset_Success()
    {
        // TribeRestriction 2 => (2 - offset 2) == tribe 0.
        var candidate = Candidate(tribeRestriction: 2);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0, EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Fact]
    public void Evaluate_TribeRestrictionMismatch_WrongTribe()
    {
        var candidate = Candidate(tribeRestriction: 2); // only tribe 0 is legal

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 1, EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.WrongTribe, outcome);
    }

    [Fact]
    public void Evaluate_SkipSlotCheckSentinel_SlotTagNeverConsulted()
    {
        var candidate = Candidate(equipPartTag: 99); // would mismatch any real slot's tag

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Theory]
    [InlineData(0, 2)] // mEquipPart[0] == 2
    [InlineData(6, 0)] // mEquipPart[6] == 0
    [InlineData(12, 14)] // mEquipPart[12] == 14
    public void Evaluate_SlotTagMatchesLookupTable_Success(int slotIndex, int expectedTag)
    {
        var candidate = Candidate(equipPartTag: expectedTag);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0, slotIndex, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Fact]
    public void Evaluate_SlotTagMismatch_WrongSlotTag()
    {
        var candidate = Candidate(equipPartTag: 2); // slot 0 expects 2

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 1, 1, 100, 0); // slot 1 expects 3

        Assert.Equal(EquipItemValidationGate.Outcome.WrongSlotTag, outcome);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(100)]
    public void Evaluate_SlotIndexOutOfBounds_WrongSlotTag(int slotIndex)
    {
        // -1 is reserved for SkipSlotCheck and covered separately; any other out-of-[0,12]-range index is
        // a defensive fallback, not expected from a caller that already bounds-checked.
        var candidate = Candidate();

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0, slotIndex, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.WrongSlotTag, outcome);
    }

    [Fact]
    public void Evaluate_CombinedLevelBelowRequirement_LevelTooLow()
    {
        var candidate = Candidate(levelLimit: 80, martialLevelLimit: 30); // requires combined level 110

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 109, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.LevelTooLow, outcome);
    }

    [Fact]
    public void Evaluate_CombinedLevelMeetsRequirement_Success()
    {
        var candidate = Candidate(levelLimit: 80, martialLevelLimit: 30);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 110, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Theory]
    [InlineData(13553, 5, false)]
    [InlineData(13553, 6, true)]
    [InlineData(33553, 5, false)]
    [InlineData(53553, 6, true)]
    public void Evaluate_RebirthSixIds_GateExactlyAtSix(int itemId, int rebirth, bool expectSuccess)
    {
        var candidate = Candidate(itemId: itemId);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, rebirth);

        Assert.Equal(expectSuccess ? EquipItemValidationGate.Outcome.Success : EquipItemValidationGate.Outcome.RebirthTooLow,
            outcome);
    }

    [Theory]
    [InlineData(13554, 11, false)]
    [InlineData(13554, 12, true)]
    [InlineData(86755, 11, false)]
    [InlineData(86757, 12, true)]
    public void Evaluate_RebirthTwelveIds_GateExactlyAtTwelve(int itemId, int rebirth, bool expectSuccess)
    {
        var candidate = Candidate(itemId: itemId);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, rebirth);

        Assert.Equal(expectSuccess ? EquipItemValidationGate.Outcome.Success : EquipItemValidationGate.Outcome.RebirthTooLow,
            outcome);
    }

    [Theory]
    [InlineData(87206, 11, false)]
    [InlineData(87213, 12, true)]
    [InlineData(87228, 11, false)]
    [InlineData(87257, 12, true)]
    public void Evaluate_RebirthTwelveIdRanges_GateExactlyAtTwelve(int itemId, int rebirth, bool expectSuccess)
    {
        var candidate = Candidate(itemId: itemId);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, rebirth);

        Assert.Equal(expectSuccess ? EquipItemValidationGate.Outcome.Success : EquipItemValidationGate.Outcome.RebirthTooLow,
            outcome);
    }

    [Theory]
    [InlineData(2303, 6, false)]
    [InlineData(2305, 7, true)]
    public void Evaluate_RebirthSevenIds_GateExactlyAtSeven(int itemId, int rebirth, bool expectSuccess)
    {
        var candidate = Candidate(itemId: itemId);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, rebirth);

        Assert.Equal(expectSuccess ? EquipItemValidationGate.Outcome.Success : EquipItemValidationGate.Outcome.RebirthTooLow,
            outcome);
    }

    [Fact]
    public void Evaluate_CheckSetItemThree_RequiresRebirthTwelve()
    {
        var candidate = Candidate(itemId: 90000, checkSetItem: 3);

        var rejected = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 11);
        var accepted = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 12);

        Assert.Equal(EquipItemValidationGate.Outcome.RebirthTooLow, rejected);
        Assert.Equal(EquipItemValidationGate.Outcome.Success, accepted);
    }

    [Fact]
    public void Evaluate_CheckSetItemOtherValue_NoRebirthGate()
    {
        var candidate = Candidate(itemId: 90000, checkSetItem: 1);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(29)]
    public void Evaluate_ItemSortClassificationGatedCodes_RequireRebirthTwelve(int classification)
    {
        var candidate = Candidate(itemId: 90000);

        var rejected = EquipItemValidationGate.Evaluate(candidate, classification, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 11);
        var accepted = EquipItemValidationGate.Evaluate(candidate, classification, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 12);

        Assert.Equal(EquipItemValidationGate.Outcome.RebirthTooLow, rejected);
        Assert.Equal(EquipItemValidationGate.Outcome.Success, accepted);
    }

    [Fact]
    public void Evaluate_ItemSortClassificationUngatedCode_NoRebirthGate()
    {
        var candidate = Candidate(itemId: 90000);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 3, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(34)]
    public void Evaluate_SortOutsideFinalWhitelist_CategoryNotEquippable(int sort)
    {
        var candidate = Candidate(sort: sort);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.CategoryNotEquippable, outcome);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(33)]
    public void Evaluate_SortAtWhitelistBounds_Success(int sort)
    {
        var candidate = Candidate(sort: sort);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 0);

        Assert.Equal(EquipItemValidationGate.Outcome.Success, outcome);
    }

    [Fact]
    public void Evaluate_FinalCategoryGate_RunsLastEvenAfterEveryOtherCheckPasses()
    {
        // Passes tribe/slot/level/rebirth but fails the final sort whitelist -- proves ordering: this must
        // still reject even though every earlier gate was satisfied.
        var candidate = Candidate(itemId: 13553, sort: 99);

        var outcome = EquipItemValidationGate.Evaluate(candidate, 0, 0,
            EquipItemValidationGate.SkipSlotCheck, 100, 6);

        Assert.Equal(EquipItemValidationGate.Outcome.CategoryNotEquippable, outcome);
    }
}
