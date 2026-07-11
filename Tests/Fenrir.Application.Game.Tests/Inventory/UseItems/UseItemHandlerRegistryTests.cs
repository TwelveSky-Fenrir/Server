using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Inventory.UseItems;
using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems;

/// <summary>
///     The op23 per-item dispatch table (<see cref="UseItemHandlerRegistry" />): the id-keyed direct
///     handlers (891/2193/8100), the loot-box handler (which now also claims 635, see C10-mountbox635),
///     the category-based equip-swap claiming a genuine equip item, and an item no family owns resolving to
///     null (the service then falls through to its generic failure).
/// </summary>
public class UseItemHandlerRegistryTests
{
    private static ItemStack Stack(int itemId)
    {
        return new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
    }

    private static ItemDefinition Def(int itemId, byte sort = 3, byte equipInfo2 = 0)
    {
        return new ItemDefinition(WorldDataTestRows.Item(itemId) with { Sort = sort, EquipInfo2 = equipInfo2 }, []);
    }

    private static (UseItemHandlerRegistry Registry, LootBoxUseItemHandler LootBox,
        TitleUpgradeUseItemHandler Title, PalaceRankUpgradeUseItemHandler Rank, EquipSwapUseItemHandler Equip)
        BuildRegistry()
    {
        var worldData = ZoneTestKit.EmptyWorldData();
        var writer = new UseItemInventoryWriter(new FakeCharacterRepository(),
            NullLogger<UseItemInventoryWriter>.Instance);
        var eventLog = new FakeEventLogRepository();

        var title = new TitleUpgradeUseItemHandler(worldData, writer, eventLog,
            NullLogger<TitleUpgradeUseItemHandler>.Instance);
        var titleRemoveScroll = new TitleRemoveScrollUseItemHandler(worldData, writer, eventLog,
            NullLogger<TitleRemoveScrollUseItemHandler>.Instance);
        var rank = new PalaceRankUpgradeUseItemHandler(worldData, writer, eventLog,
            NullLogger<PalaceRankUpgradeUseItemHandler>.Instance);
        var equip = new EquipSwapUseItemHandler(worldData, writer, NullLogger<EquipSwapUseItemHandler>.Instance);
        var lootBox = new LootBoxUseItemHandler(worldData, new FakeCharacterRepository(), eventLog,
            NullLogger<LootBoxUseItemHandler>.Instance);
        var forcedNeutralTribeReset = new ForcedNeutralTribeResetUseItemHandler(new FakeCharacterRepository(),
            writer, new FakeGameServerDirectoryRepository(), new FakeShardMapAssignmentRepository(
                new Dictionary<byte, short[]>()), new GameServerOptions(),
            NullLogger<ForcedNeutralTribeResetUseItemHandler>.Instance);
        var tribeScrollTransfer = new TribeScrollTransferUseItemHandler(new FakeTribeConversionRepository(),
            new TribeConversionResolver([], [], []), new PartyRegistry(), new FakeGameServerDirectoryRepository(),
            new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>()), worldData, new GameServerOptions(),
            NullLogger<TribeScrollTransferUseItemHandler>.Instance);

        // C9-tickets-tower: no test in this file exercises these 6 handlers' own routing yet -- they only
        // need to exist so the registry's constructor is satisfied.
        var cpTicket = new CpTicketUseItemHandler(writer, eventLog, NullLogger<CpTicketUseItemHandler>.Instance);
        var eliteDungeonTicket = new EliteDungeonTicketUseItemHandler(writer, eventLog,
            NullLogger<EliteDungeonTicketUseItemHandler>.Instance);
        var dungeonKey = new DungeonKeyUseItemHandler(writer, eventLog,
            NullLogger<DungeonKeyUseItemHandler>.Instance);
        var ivyHallTicket = new IvyHallTicketUseItemHandler(writer, eventLog,
            NullLogger<IvyHallTicketUseItemHandler>.Instance);
        var luckyTicket = new LuckyTicketUseItemHandler(worldData, writer,
            NullLogger<LuckyTicketUseItemHandler>.Instance);
        var scrollOfSeekers =
            new ScrollOfSeekersUseItemHandler(writer, eventLog, NullLogger<ScrollOfSeekersUseItemHandler>.Instance);
        var costumeStellarCore = new CostumeStellarCoreUseItemHandler(writer, eventLog,
            NullLogger<CostumeStellarCoreUseItemHandler>.Instance);

        return (new UseItemHandlerRegistry(title, titleRemoveScroll, rank, equip, lootBox, forcedNeutralTribeReset,
                tribeScrollTransfer, cpTicket, eliteDungeonTicket, dungeonKey, ivyHallTicket, luckyTicket,
                scrollOfSeekers, costumeStellarCore),
            lootBox, title, rank, equip);
    }

    [Fact]
    public void Resolve_TitleTicket891_RoutesToTitleHandler()
    {
        var (registry, _, title, _, _) = BuildRegistry();
        Assert.Same(title, registry.Resolve(Stack(891), Def(891)));
    }

    [Fact]
    public void Resolve_PalaceRankTicket2193_RoutesToRankHandler()
    {
        var (registry, _, _, rank, _) = BuildRegistry();
        Assert.Same(rank, registry.Resolve(Stack(2193), Def(2193)));
    }

    [Theory]
    [InlineData(1200)]
    [InlineData(8419)]
    [InlineData(1494)]
    public void Resolve_TitleRemoveScroll_RoutesToTitleRemoveScrollHandler(int itemId)
    {
        var (registry, _, _, _, _) = BuildRegistry();
        Assert.IsType<TitleRemoveScrollUseItemHandler>(registry.Resolve(Stack(itemId), Def(itemId)));
    }

    [Theory]
    [InlineData(8153)]
    [InlineData(8154)]
    public void Resolve_FactionTransferScroll_RoutesToTribeScrollTransferHandler(int itemId)
    {
        var (registry, _, _, _, _) = BuildRegistry();
        Assert.IsType<TribeScrollTransferUseItemHandler>(registry.Resolve(Stack(itemId), Def(itemId)));
    }

    [Fact]
    public void Resolve_MountBox635_RoutesToLootBoxHandler()
    {
        // C10-mountbox635: item 635 is now a fully-populated LootBoxCatalog entry (uniform 1-in-8 over eight
        // tier-3 mount ids), so it routes through the shared loot-box handler like every other
        // catalog-registered box, not a dedicated per-item stub (the former MountBoxUseItemHandler, deleted).
        var (registry, lootBox, _, _, _) = BuildRegistry();
        Assert.Same(lootBox, registry.Resolve(Stack(635), Def(635)));
    }

    [Fact]
    public void Resolve_EquipItem_RoutesToEquipSwapHandler()
    {
        var (registry, _, _, _, equip) = BuildRegistry();
        Assert.Same(equip, registry.Resolve(Stack(1000), Def(1000, sort: 6, equipInfo2: 2)));
    }

    [Fact]
    public void Resolve_UnclaimedItem_ReturnsNull()
    {
        var (registry, _, _, _, _) = BuildRegistry();
        Assert.Null(registry.Resolve(Stack(999999), Def(999999, sort: 3)));
    }

    [Fact]
    public void Resolve_ValidCostumeItem_RoutesToCostumeStellarCoreHandler()
    {
        // C9-costume-stellar-whitelist: 301 is the low end of IsValidCostume's live table -- Sort 3 (this
        // file's own Def() default) is well outside the equip category range, so only the new third fallback
        // can claim it.
        var (registry, _, _, _, _) = BuildRegistry();
        Assert.IsType<CostumeStellarCoreUseItemHandler>(registry.Resolve(Stack(301), Def(301)));
    }

    [Fact]
    public void Resolve_ValidStellarCoreItem_RoutesToCostumeStellarCoreHandler()
    {
        // 76527 is the low end of IsValidStellarCore's live table.
        var (registry, _, _, _, _) = BuildRegistry();
        Assert.IsType<CostumeStellarCoreUseItemHandler>(registry.Resolve(Stack(76527), Def(76527)));
    }

    [Fact]
    public void Resolve_IdKeyedHandlerWinsOverCategory_EvenIfItAlsoLooksEquip()
    {
        // An id-keyed family (891) must resolve to its direct handler even if its catalog row happened to
        // carry an equip-shaped Sort/tag -- the id table is consulted before the equip predicate.
        var (registry, _, title, _, _) = BuildRegistry();
        Assert.Same(title, registry.Resolve(Stack(891), Def(891, sort: 6, equipInfo2: 2)));
    }
}
