using Fenrir.Application.Game.Domain.Enchant;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Enchant;

public class EnchantResolverAbsoluteProtectTests
{
    private static ItemDefinition Equip(int itemId, byte sort = 7, byte type = 3, byte checkImprove = 2)
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
    public void FailAbove41_AbsoluteTicketAvailable_LeavesEnchantCompletelyUntouched()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(45), Material(1023), 0, 0,
            0, new ScriptedRandomSource(99), protectForDestroy2Charges: 1);

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(45, result.NewEnchant);
        Assert.True(result.ConsumesProtectCharge2);
        Assert.False(result.ConsumesProtectCharge);
    }

    [Fact]
    public void FailAbove41_BothProtectResourcesAvailable_AbsoluteTicketTakesPriority()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(45), Material(1023), 0, 1,
            0, new ScriptedRandomSource(99), protectForDestroy2Charges: 1);

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(45, result.NewEnchant);
        Assert.True(result.ConsumesProtectCharge2);
        Assert.False(result.ConsumesProtectCharge);
    }

    [Fact]
    public void FailAbove41_OnlyOrdinaryProtectAvailable_FallsBackAndDecrementsByOne()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(45), Material(1023), 0, 1,
            0, new ScriptedRandomSource(99), protectForDestroy2Charges: 0);

        Assert.Equal(EnchantResolver.EnchantOutcome.Protected, result.Outcome);
        Assert.Equal(44, result.NewEnchant);
        Assert.True(result.ConsumesProtectCharge);
        Assert.False(result.ConsumesProtectCharge2);
    }

    [Fact]
    public void FailAtExactly41_AbsoluteTicketNeverConsulted_UnconditionalResetToForty()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(41), Material(1023), 0, 0,
            0, new ScriptedRandomSource(20), protectForDestroy2Charges: 1);

        Assert.Equal(EnchantResolver.EnchantOutcome.ResetToForty, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
        Assert.False(result.ConsumesProtectCharge2);
    }

    [Fact]
    public void FailAbove41_NoProtectResourceAvailable_ResetsToForty()
    {
        var result = EnchantResolver.Resolve(Equip(1), Target(45), Material(1023), 0, 0,
            0, new ScriptedRandomSource(99), protectForDestroy2Charges: 0);

        Assert.Equal(EnchantResolver.EnchantOutcome.ResetToForty, result.Outcome);
        Assert.Equal(40, result.NewEnchant);
        Assert.False(result.ConsumesProtectCharge2);
    }
}
