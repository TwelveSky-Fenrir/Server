using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

/// <summary>
///     End-to-end (but pure, I/O-free) coverage for <see cref="LootBoxOpenResolver" /> over the real
///     <see cref="LootBoxCatalog" /> specs: single opens (rare-band hit, pool path, rental stamp, inventory
///     full, unknown reward) and bulk opens (count clamp, box consumption, no-progress stop). The reward-and-box
///     atomicity contract is asserted through the projected pages -- a reward slot is only ever populated
///     alongside the box slot being decremented, never one without the other.
/// </summary>
public class LootBoxOpenResolverTests
{
    private const int Today = 20260710;
    private static readonly ItemStack Filler = new(999, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly BoxRewardSpec MountBox = LootBoxCatalog.Default.TryGetSpec(601)!;
    private static readonly BoxRewardSpec LimitedStellar = LootBoxCatalog.Default.TryGetSpec(76542)!;

    private static ItemStack Box(int id, int quantity)
    {
        return new ItemStack(id, quantity, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static Func<int, byte?> Sorts(params (int Id, int Sort)[] rows)
    {
        var map = rows.ToDictionary(r => r.Id, r => (byte)r.Sort);
        return id => map.TryGetValue(id, out var sort) ? sort : null;
    }

    [Fact]
    public void OpenSingle_RareBandHit_PlacesNonStackableReward_AndConsumesExactlyOneBox()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 3));

        // Rare draw 49 (< 50) -> id 635; 635 resolves to a non-stackable sort (weapon) -> quantity 0.
        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 3), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((635, 4)), new ScriptedRandom(49), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(635, plan.RewardItemId);
        Assert.Equal(0, plan.RewardQuantity);
        Assert.Equal(BoxRewardPlacementResolver.Outcome.PlacedInEmptySlot, plan.PlacementOutcome);
        Assert.Equal(ContainerMatrix.InventoryPage0, plan.RewardContainer);
        Assert.Equal(1, plan.RewardSlot);
        Assert.Equal(2, plan.BoxRemainingQuantity);

        Assert.Equal(2, plan.ProjectedPage0[0].Quantity); // box decremented by exactly one
        Assert.Equal(635, plan.ProjectedPage0[1].ItemId); // reward placed
    }

    [Fact]
    public void OpenSingle_PoolPath_PlacesPetReward_ClampedToActivityOne()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 1));

        // Rare miss (50), pool-select 5 (<= 10) -> pool0 {92286}, within-pool uniform 0 -> 92286 (a pet sort).
        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 1), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((92286, BoxRewardPlacementResolver.PetSort)),
            new ScriptedRandom(50, 5, 0), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(92286, plan.RewardItemId);
        Assert.Equal(1, plan.RewardQuantity);
        Assert.Equal(0, plan.BoxRemainingQuantity); // last box consumed
        Assert.False(plan.ProjectedPage0.ContainsKey(0)); // box slot cleared at zero
        Assert.Equal(92286, plan.ProjectedPage0[1].ItemId);
    }

    [Fact]
    public void OpenSingle_RentalBox_StampsProjectedExpiryOnNonStackableReward()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(76542, 1));

        // Uniform draw 0 -> 76534 (a non-stackable costume sort); 76542 grants a 3-day rental.
        var plan = LootBoxOpenResolver.OpenSingle(LimitedStellar, 0, 0, Box(76542, 1), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((76534, 30)), new ScriptedRandom(0), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(76534, plan.RewardItemId);
        Assert.Equal(20260713, plan.RewardStack.ExpireDate);
        Assert.Equal(20260713, plan.ProjectedPage0[1].ExpireDate);
    }

    [Fact]
    public void OpenSingle_ResolveRewardSerial_StampsTheOverrideSerial_OnANonStackableReward()
    {
        // Workstream lucky-ticket-handler-thresholds: resolveRewardSerial lets a caller stamp a fixed serial
        // (e.g. the Lucky Ticket family's per-ticket constant) instead of the default 0 -- every pre-existing
        // box (like this same MountBox spec used elsewhere in this file) keeps Serial 0 unless it opts in.
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 1));

        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 1), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((635, 4)), new ScriptedRandom(49), Today,
            resolveRewardSerial: _ => 100000001);

        Assert.Equal(LootBoxOpenResolver.Outcome.Success, plan.Outcome);
        Assert.Equal(635, plan.RewardItemId);
        Assert.Equal(100000001, plan.RewardStack.Serial);
        Assert.Equal(100000001, plan.ProjectedPage0[1].Serial);
    }

    [Fact]
    public void OpenSingle_NoResolveRewardSerial_DefaultsToZero()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 3));

        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 3), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((635, 4)), new ScriptedRandom(49), Today);

        Assert.Equal(0, plan.RewardStack.Serial);
    }

    [Fact]
    public void OpenSingle_InventoryFull_ReportsFull_AndDoesNotConsumeTheBox()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        builder[0] = Box(601, 2);
        for (byte slot = 1; slot <= 63; slot++)
            builder[slot] = Filler;
        var fullExceptBox = builder.ToImmutable();

        var page1Full = FullPage();

        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 2), fullExceptBox, page1Full,
            Sorts((635, 4)), new ScriptedRandom(49), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.InventoryFull, plan.Outcome);
        Assert.False(plan.Succeeded);
    }

    [Fact]
    public void OpenSingle_RewardIdNotInItemMaster_ReportsRewardNotFound_AndDoesNotConsumeTheBox()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 2));

        var plan = LootBoxOpenResolver.OpenSingle(MountBox, 0, 0, Box(601, 2), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts(), new ScriptedRandom(49), Today);

        Assert.Equal(LootBoxOpenResolver.Outcome.RewardNotFound, plan.Outcome);
        Assert.False(plan.Succeeded);
    }

    [Fact]
    public void OpenBulk_OpensRequestedCount_ConsumingOneBoxPerOpen()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 5));

        var plan = LootBoxOpenResolver.OpenBulk(MountBox, 0, 0, Box(601, 5), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((635, 4)), new ScriptedRandom(49, 49, 49), Today,
            requestedCount: 3);

        Assert.Equal(3, plan.OpenedCount);
        Assert.Equal(2, plan.BoxRemainingQuantity);
        Assert.Equal(2, plan.ProjectedPage0[0].Quantity);
        Assert.Equal(3, plan.Rewards.Length);
        Assert.Equal(635, plan.ProjectedPage0[1].ItemId);
        Assert.Equal(635, plan.ProjectedPage0[3].ItemId);
    }

    [Fact]
    public void OpenBulk_CountClampedToBoxStock()
    {
        var page0 = ImmutableDictionary<byte, ItemStack>.Empty.SetItem(0, Box(601, 4));

        var plan = LootBoxOpenResolver.OpenBulk(MountBox, 0, 0, Box(601, 4), page0,
            ImmutableDictionary<byte, ItemStack>.Empty, Sorts((635, 4)), new ScriptedRandom(49, 49, 49, 49), Today,
            requestedCount: 100);

        Assert.Equal(4, plan.OpenedCount);
        Assert.Equal(0, plan.BoxRemainingQuantity);
    }

    [Fact]
    public void OpenBulk_StopsEarly_WhenInventoryFills_WithoutInfiniteLoop()
    {
        // Two free slots total (page0 slot63, page1 slot63); the box sits in page0 slot0.
        var page0Builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        page0Builder[0] = Box(601, 5);
        for (byte slot = 1; slot <= 62; slot++)
            page0Builder[slot] = Filler;

        var page1Builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        for (byte slot = 0; slot <= 62; slot++)
            page1Builder[slot] = Filler;

        var plan = LootBoxOpenResolver.OpenBulk(MountBox, 0, 0, Box(601, 5), page0Builder.ToImmutable(),
            page1Builder.ToImmutable(), Sorts((635, 4)), new ScriptedRandom(49, 49, 49), Today, requestedCount: 5);

        Assert.Equal(2, plan.OpenedCount); // two free slots filled, the third open finds none and halts
        Assert.Equal(3, plan.BoxRemainingQuantity);
    }

    private static ImmutableDictionary<byte, ItemStack> FullPage()
    {
        var builder = ImmutableDictionary.CreateBuilder<byte, ItemStack>();
        for (byte slot = 0; slot <= 63; slot++)
            builder[slot] = Filler;
        return builder.ToImmutable();
    }

    /// <summary>Returns queued draws in request order; throws if the code draws more than were scripted.</summary>
    private sealed class ScriptedRandom(params int[] values) : Random
    {
        private int _index;

        public override int Next(int minValue, int maxValue)
        {
            if (_index >= values.Length)
                throw new InvalidOperationException("ScriptedRandom exhausted: the code drew more values than scripted.");

            return values[_index++];
        }
    }
}
