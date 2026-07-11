using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

public class LootBoxOpenResolverVaultGateTests
{
    private const int Today = 20260710;
    private static readonly ItemStack Filler = new(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly BoxRewardSpec MountBox = LootBoxCatalog.Default.TryGetSpec(601)!;

    private static ItemStack Box(int id, int quantity)
    {
        return new ItemStack(id, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static Func<int, byte?> Sorts(params (int Id, int Sort)[] rows)
    {
        var map = rows.ToDictionary(r => r.Id, r => (byte)r.Sort);
        return id => map.TryGetValue(id, out var sort) ? sort : null;
    }

    private static ImmutableDictionary<byte, ItemStack> FullPageExceptBoxSlot()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        builder[0] = Box(601, 3);
        for (byte slot = 1; slot <= 63; slot++)
            builder[slot] = Filler;
        return builder.ToImmutable();
    }

    [Fact]
    public void OpenSingle_SecondPageInaccessible_Page0Full_ReportsInventoryFull_EvenThoughPage1HasRoom()
    {
        var page0 = FullPageExceptBoxSlot();
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 3), page0, page1, Sorts((635, 4)),
            new ScriptedRandom(49), Today, secondPageAccessible: false);

        Assert.Equal(LootBoxOpenResolver.Outcome.InventoryFull, plan.Outcome);
        Assert.False(plan.Succeeded);
    }

    [Fact]
    public void OpenSingle_SecondPageAccessible_Page0Full_StillPlacesOnPage1_MatchingDefaultBehavior()
    {
        var page0 = FullPageExceptBoxSlot();
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 3), page0, page1, Sorts((635, 4)),
            new ScriptedRandom(49), Today, secondPageAccessible: true);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(ContainerMatrix.InventoryPage1, plan.RewardContainer);
    }

    [Fact]
    public void OpenBulk_SecondPageInaccessible_NeverSpillsOntoPage1_EvenAcrossMultipleAttemptedOpens()
    {
        var page0 = FullPageExceptBoxSlot();
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        var plan = LootBoxOpenResolver.OpenBulk(MountBox, 0, 0, Box(601, 3), page0, page1, Sorts((635, 4)),
            new ScriptedRandom(49, 49, 49), Today, 3, secondPageAccessible: false);

        Assert.Equal(0, plan.OpenedCount);
        Assert.True(plan.ProjectedPage1.IsEmpty);
    }

    private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException(
                    "ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
