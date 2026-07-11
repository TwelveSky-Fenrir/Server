using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

/// <summary>
///     Covers the wing-only Protection material 8106 (2026-07-11 supplemental finding,
///     enchant-resolver-wing-8106-ticket): flat +1 enchant value, a flat 50-CP Wing-category cost charged
///     win-or-lose, and a NoChange (ZC result 9) short-circuit on failure that never risks a downgrade or
///     destroy -- see <see cref="EnchantResolver" />'s own remarks and <see cref="WingEnchantMaterialWhitelist" />.
/// </summary>
public class EnchantResolverWingProtectedMaterialTests
{
    private const int ProtectedMaterialId = WingEnchantMaterialWhitelist.ProtectedMaterialItemId;

    private static ItemDefinition Equip(int itemId, byte sort = 6, byte type = 0, byte checkImprove = 2)
    {
        var row = WorldDataTestRows.Item(itemId) with { Sort = sort, Type = type, CheckImprove = checkImprove };
        return new ItemDefinition(row, []);
    }

    private static ItemDefinition Material(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId), []);
    }

    private static ItemStack Target(byte enchant)
    {
        return new ItemStack(1, 1, enchant, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    [Fact]
    public void SuccessRoll_IncrementsByOne_CostsFlatFiftyCp_FlagsIsWing()
    {
        // current=0, +1 => new=1, p1 = 103 - 3 = 100; roll 0 always succeeds.
        var result = EnchantResolver.Resolve(Equip(1), Target(0), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Success, result.Outcome);
        Assert.Equal(1, result.NewEnchant);
        Assert.Equal(WingEnchantMaterialWhitelist.WingEnchantCpCost, result.Cost);
        Assert.True(result.IsWing);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void FailureRoll_BelowSafeValue_LeavesEnchantUntouched_ChargesFlatCp()
    {
        // current=10, +1 => new=11, p1 = 103 - 33 = 70; roll 99 fails the success roll.
        var result = EnchantResolver.Resolve(Equip(1), Target(10), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(10, result.NewEnchant);
        Assert.Equal(WingEnchantMaterialWhitelist.WingEnchantCpCost, result.Cost);
        Assert.True(result.ConsumesMaterial);
    }

    [Fact]
    public void FailureRoll_AboveSafeValue_NeverDestroys_LeavesEnchantUntouched()
    {
        // current=30, +1 => new=31 (> SafeImproveValue=20): an ordinary stone would roll for destruction.
        // 8106 short-circuits before any destroy-risk check -- a single failing roll always yields NoChange.
        var result = EnchantResolver.Resolve(Equip(1), Target(30), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(99));

        Assert.Equal(EnchantResolver.EnchantOutcome.NoChange, result.Outcome);
        Assert.Equal(30, result.NewEnchant);
    }

    [Fact]
    public void ImproveCharge_ConsumedOnRolledSuccessAndFailure()
    {
        var success = EnchantResolver.Resolve(Equip(1), Target(0), Material(ProtectedMaterialId), 0, 0,
            1, new ScriptedRandomSource(0));
        Assert.True(success.ConsumesImproveCharge);

        var failure = EnchantResolver.Resolve(Equip(1), Target(10), Material(ProtectedMaterialId), 0, 0,
            1, new ScriptedRandomSource(99));
        Assert.True(failure.ConsumesImproveCharge);
    }

    [Fact]
    public void NonWingTarget_IsRejected_MaterialIsWingExclusive()
    {
        // Sort 7 (ordinary equipment, not a wing): 8106 is not in EnchantMaterialCatalog.StandardMaterials,
        // so a non-wing target enchanted with it is rejected rather than silently treated as a stone.
        var result = EnchantResolver.Resolve(Equip(1, sort: 7), Target(0), Material(ProtectedMaterialId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void SiblingMaterial695_FailurePathUnresolved_StaysRejected()
    {
        // 695 shares 8106's +1 enchant-value assignment, but its own FAILURE path was never observed by the
        // finding that recovered that value (a different code block than 8106's) -- left deliberately
        // unmodeled rather than guessed. See WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId.
        var result = EnchantResolver.Resolve(Equip(1),
            Target(0), Material(WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId), 0, 0,
            0, new ScriptedRandomSource(0));

        Assert.Equal(EnchantResolver.EnchantOutcome.Rejected, result.Outcome);
    }
}
