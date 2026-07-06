using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;

namespace Fenrir.Application.Game.Tests.Inventory;

/// <summary>
///     Coverage for <see cref="InventoryToWorldDropPolicy" />, the pure policy behind tSort 209
///     ("drop item from inventory to the ground at the player's own location"). Exercises the static
///     <see cref="InventoryToWorldDropPolicy.Resolve" /> method directly against hand-built
///     <see cref="ItemStack" />/<see cref="ItemDefinition" /> values -- does not depend on any dispatch wiring,
///     <c>Zone</c>, or DI container.
/// </summary>
public class InventoryToWorldDropPolicyTests
{
    private static readonly Func<ItemDefinition, GroundItemSpawnEligibility> AlwaysEligible =
        static _ => GroundItemSpawnEligibility.Eligible;

    private static ItemDefinition StackableItem(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 2 }, []);
    }

    private static ItemDefinition UniqueItem(int itemId)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = 9 }, []);
    }

    private static ItemStack Stack(int itemId, int quantity, byte enchant = 0, byte combine = 0, byte refine = 0,
        byte socket = 0, int gem1 = 0, int gem2 = 0, int gem3 = 0, int expireDate = 0, int serial = 0)
    {
        return new ItemStack(itemId, quantity, enchant, combine, refine, socket, gem1, gem2, gem3, expireDate,
            serial);
    }

    private static InventoryToWorldDropPolicy.Result Resolve(
        int sourcePage = ContainerMatrix.InventoryPage0,
        int sourceSlot = 0,
        int requestedQuantity = 0,
        bool premiumPageAccessAllowed = true,
        ItemStack? source = null,
        ItemDefinition? itemDefinition = null,
        bool sourceIsDroppableByPlayer = true,
        Func<ItemDefinition, GroundItemSpawnEligibility>? evaluateSpawnEligibility = null,
        int currentGroundItemCount = 0,
        float dropperPosX = 10f, float dropperPosY = 1f, float dropperPosZ = 20f,
        string dropperName = "Hero", string? dropperPartyName = null)
    {
        return InventoryToWorldDropPolicy.Resolve(sourcePage, sourceSlot, requestedQuantity,
            premiumPageAccessAllowed, source, itemDefinition, sourceIsDroppableByPlayer,
            evaluateSpawnEligibility ?? AlwaysEligible, currentGroundItemCount, dropperPosX, dropperPosY,
            dropperPosZ, dropperName, dropperPartyName);
    }

    // ---- Malformed input (caller must disconnect) ----

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(2, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 64)]
    public void OutOfRangePageOrSlot_IsMalformed(int page, int slot)
    {
        var result = Resolve(sourcePage: page, sourceSlot: slot, source: Stack(50, 1),
            itemDefinition: StackableItem(50));

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.SourceOutOfRange, result.Outcome);
        Assert.True(result.IsMalformed);
        Assert.False(result.Succeeded);
        Assert.Null(result.Spawn);
    }

    [Fact]
    public void SecondPageExpired_IsMalformed()
    {
        var result = Resolve(sourcePage: ContainerMatrix.InventoryPage1, premiumPageAccessAllowed: false,
            source: Stack(50, 1), itemDefinition: StackableItem(50));

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.PremiumPageExpired, result.Outcome);
        Assert.True(result.IsMalformed);
    }

    [Fact]
    public void FirstPage_IgnoresPremiumGate_EvenWhenExpired()
    {
        var result = Resolve(sourcePage: ContainerMatrix.InventoryPage0, premiumPageAccessAllowed: false,
            source: Stack(50, 1), itemDefinition: StackableItem(50));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void EmptySourceSlot_IsMalformed_AsUnknownItem()
    {
        var result = Resolve(source: null, itemDefinition: null);

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.UnknownItem, result.Outcome);
        Assert.True(result.IsMalformed);
    }

    [Fact]
    public void SourceItemIdDoesNotResolveInCatalog_IsMalformed_AsUnknownItem()
    {
        var result = Resolve(source: Stack(999, 1), itemDefinition: null);

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.UnknownItem, result.Outcome);
        Assert.True(result.IsMalformed);
    }

    [Fact]
    public void NonDroppableItem_IsMalformed()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50),
            sourceIsDroppableByPlayer: false);

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.NonDroppableItem, result.Outcome);
        Assert.True(result.IsMalformed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    public void StackableQuantityOutsideValidRange_IsMalformed(int quantity)
    {
        var result = Resolve(requestedQuantity: quantity, source: Stack(50, 500), itemDefinition: StackableItem(50));

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.QuantityOutOfRange, result.Outcome);
        Assert.True(result.IsMalformed);
    }

    [Fact]
    public void StackableQuantityGreaterThanHeld_IsMalformed()
    {
        var result = Resolve(requestedQuantity: 51, source: Stack(50, 50), itemDefinition: StackableItem(50));

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.InsufficientQuantity, result.Outcome);
        Assert.True(result.IsMalformed);
    }

    [Fact]
    public void UniqueItem_RequestedQuantityNeverValidated_EvenWhenWildlyOutOfRange()
    {
        var result = Resolve(requestedQuantity: 999_999, source: Stack(700, 1), itemDefinition: UniqueItem(700));

        Assert.True(result.Succeeded);
    }

    // ---- Soft failure (standard ack, code 1; source untouched; no disconnect) ----

    [Fact]
    public void UnsupportedItemType_IsSoftFailure()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50),
            evaluateSpawnEligibility: static _ => GroundItemSpawnEligibility.UnsupportedItemType);

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.UnsupportedItemType, result.Outcome);
        Assert.True(result.IsSoftFailure);
        Assert.False(result.IsMalformed);
        Assert.Null(result.Spawn);
    }

    [Fact]
    public void InvalidPackedValue_IsSoftFailure()
    {
        var result = Resolve(source: Stack(700, 1), itemDefinition: UniqueItem(700),
            evaluateSpawnEligibility: static _ => GroundItemSpawnEligibility.InvalidPackedValue);

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.InvalidPackedValue, result.Outcome);
        Assert.True(result.IsSoftFailure);
    }

    [Fact]
    public void GroundItemTableAtCapacity_IsSoftFailure()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50),
            currentGroundItemCount: InventoryToWorldDropPolicy.GroundItemCapacity);

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.GroundItemTableFull, result.Outcome);
        Assert.True(result.IsSoftFailure);
    }

    [Fact]
    public void GroundItemTableBelowCapacity_Succeeds()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50),
            currentGroundItemCount: InventoryToWorldDropPolicy.GroundItemCapacity - 1);

        Assert.True(result.Succeeded);
    }

    // ---- Success: stackable partial/full drop ----

    [Fact]
    public void StackablePartialDrop_ReducesSourceQuantityOnly_RestUnchanged()
    {
        var source = Stack(50, 10, enchant: 3, serial: 777);
        var result = Resolve(requestedQuantity: 4, source: source, itemDefinition: StackableItem(50));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.NewSource);
        Assert.Equal(6, result.NewSource!.Value.Quantity);
        Assert.Equal(3, result.NewSource.Value.Enchant);
        Assert.Equal(777, result.NewSource.Value.Serial);

        Assert.NotNull(result.Spawn);
        Assert.Equal(50, result.Spawn!.Value.ItemId);
        Assert.Equal(4, result.Spawn.Value.Quantity);
        Assert.Equal(GroundItemEntity.ManualGroundDropSort, result.Spawn.Value.DropSort);
    }

    [Fact]
    public void StackableFullStackDrop_ClearsSourceSlotEntirely()
    {
        var result = Resolve(requestedQuantity: 10, source: Stack(50, 10), itemDefinition: StackableItem(50));

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
        Assert.Equal(10, result.Spawn!.Value.Quantity);
    }

    [Fact]
    public void StackableZeroQuantityDrop_SpawnsOneUnit_ButNeverTouchesSourceStack()
    {
        // Faithfully-reproduced legacy duplication quirk: 0 is neither negative/over-cap nor "more than held",
        // so it sails through every malformed check, and the shared spawn routine substitutes exactly 1 unit.
        var result = Resolve(requestedQuantity: 0, source: Stack(50, 25), itemDefinition: StackableItem(50));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.NewSource);
        Assert.Equal(25, result.NewSource!.Value.Quantity); // untouched
        Assert.Equal(1, result.Spawn!.Value.Quantity); // ground copy still gets exactly 1 unit
    }

    [Fact]
    public void StackableZeroQuantityDrop_IsRepeatable_WithoutEverDepletingTheSourceStack()
    {
        var source = Stack(50, 1);
        var itemDefinition = StackableItem(50);

        var first = Resolve(requestedQuantity: 0, source: source, itemDefinition: itemDefinition);
        Assert.True(first.Succeeded);
        Assert.Equal(1, first.NewSource!.Value.Quantity);

        var second = Resolve(requestedQuantity: 0, source: first.NewSource, itemDefinition: itemDefinition);
        Assert.True(second.Succeeded);
        Assert.Equal(1, second.NewSource!.Value.Quantity);
        Assert.Equal(1, second.Spawn!.Value.Quantity);
    }

    [Fact]
    public void StackableDrop_NeverCarriesSocketDataToGroundCopy()
    {
        var result = Resolve(requestedQuantity: 5,
            source: Stack(50, 10, enchant: 9, combine: 2, refine: 1, socket: 3, gem1: 111, gem2: 222, gem3: 333),
            itemDefinition: StackableItem(50));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.Spawn!.Value.Value);
        Assert.Equal(0, result.Spawn.Value.SocketGem1);
        Assert.Equal(0, result.Spawn.Value.SocketGem2);
        Assert.Equal(0, result.Spawn.Value.SocketGem3);
        Assert.Equal(0, result.Spawn.Value.SerialNumber);
    }

    // ---- Success: unique item drop ----

    [Fact]
    public void UniqueItemDrop_AlwaysClearsSourceSlot_RegardlessOfRequestedQuantity()
    {
        var result = Resolve(requestedQuantity: 1, source: Stack(700, 1), itemDefinition: UniqueItem(700));

        Assert.True(result.Succeeded);
        Assert.Null(result.NewSource);
    }

    [Fact]
    public void UniqueItemDrop_CarriesEnchantAndSocketDataToGroundCopy()
    {
        var source = Stack(700, 1, enchant: 12, combine: 3, refine: 1, socket: 2, gem1: 501, gem2: 502, gem3: 503,
            serial: 909);
        var result = Resolve(source: source, itemDefinition: UniqueItem(700));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Spawn);
        Assert.Equal(700, result.Spawn!.Value.ItemId);
        Assert.Equal(1, result.Spawn.Value.Quantity);
        Assert.Equal(909, result.Spawn.Value.SerialNumber);
        Assert.Equal(501, result.Spawn.Value.SocketGem1);
        Assert.Equal(502, result.Spawn.Value.SocketGem2);
        Assert.Equal(503, result.Spawn.Value.SocketGem3);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(result.Spawn.Value.Value);
        Assert.Equal(12, enchant);
        Assert.Equal(3, combine);
        Assert.Equal(1, refine);
        Assert.Equal(2, socket);
    }

    // ---- Owner resolution ----

    [Fact]
    public void Unpartied_OwnerIsDropperOwnName()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50), dropperName: "Solo",
            dropperPartyName: null);

        Assert.Equal("Solo", result.Spawn!.Value.Owner);
    }

    [Fact]
    public void Partied_OwnerIsPartyName_NotDropperOwnName()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50), dropperName: "Solo",
            dropperPartyName: "TheLeader");

        Assert.Equal("TheLeader", result.Spawn!.Value.Owner);
    }

    [Fact]
    public void EmptyPartyName_TreatedAsUnpartied()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50), dropperName: "Solo",
            dropperPartyName: "");

        Assert.Equal("Solo", result.Spawn!.Value.Owner);
    }

    // ---- Position pass-through ----

    [Fact]
    public void GroundSpawnPosition_MatchesDropperCurrentPosition()
    {
        var result = Resolve(source: Stack(50, 1), itemDefinition: StackableItem(50), dropperPosX: 123f,
            dropperPosY: 45f, dropperPosZ: 678f);

        Assert.Equal(123f, result.Spawn!.Value.PosX);
        Assert.Equal(45f, result.Spawn.Value.PosY);
        Assert.Equal(678f, result.Spawn.Value.PosZ);
    }

    // ---- Eligibility predicate receives the resolved item definition ----

    [Fact]
    public void EligibilityPredicate_IsInvokedWithTheResolvedItemDefinition()
    {
        ItemDefinition? seen = null;
        var itemDefinition = StackableItem(50);

        Resolve(source: Stack(50, 1), itemDefinition: itemDefinition, evaluateSpawnEligibility: def =>
        {
            seen = def;
            return GroundItemSpawnEligibility.Eligible;
        });

        Assert.Same(itemDefinition, seen);
    }

    [Fact]
    public void EligibilityPredicate_IsNotInvoked_WhenAnEarlierMalformedCheckAlreadyFailed()
    {
        var invoked = false;

        var result = Resolve(sourcePage: 5, source: Stack(50, 1), itemDefinition: StackableItem(50),
            evaluateSpawnEligibility: _ =>
            {
                invoked = true;
                return GroundItemSpawnEligibility.Eligible;
            });

        Assert.Equal(InventoryToWorldDropPolicy.Outcome.SourceOutOfRange, result.Outcome);
        Assert.False(invoked);
    }
}
