using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

/// <summary>
///     C1-vault-expiry-enforcement, trigger 2: covers <see cref="LootBoxOpenResolver" />'s new
///     <c>secondPageAccessible</c> parameter (threaded through to <see cref="BoxRewardPlacementResolver.Resolve" />).
///     Companion to <see cref="LootBoxOpenResolverTests" />, which only exercises the always-accessible
///     (default) shape.
/// </summary>
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
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty; // fully empty -- would normally absorb the reward

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
        // Every slot on page0 is occupied (slot 0 by the box itself, 1-63 by Filler) -- with page1 excluded,
        // there is no valid placement target for any of the requested opens, and no-progress must halt
        // immediately rather than looping.
        var page0 = FullPageExceptBoxSlot();
        var page1 = ImmutableDictionary<byte, ItemStack>.Empty;

        var plan = LootBoxOpenResolver.OpenBulk(MountBox, 0, 0, Box(601, 3), page0, page1, Sorts((635, 4)),
            new ScriptedRandom(49, 49, 49), Today, requestedCount: 3, secondPageAccessible: false);

        Assert.Equal(0, plan.OpenedCount);
        Assert.True(plan.ProjectedPage1.IsEmpty);
    }

    /// <summary>Returns queued draws in request order; throws if the code draws more than were scripted.</summary>
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
