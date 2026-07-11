using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Services.ZoneLifecycle;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Handlers;

/// <summary>
///     A11 (wave15 dispatch-wiring fix) -- covers the previously-confirmed gap: <c>UseInventoryItemService.cs</c>
///     had zero references to catalog items 665/667, so a tower-construct/heal request fell through to the
///     generic Result=1 "unhandled item" failure instead of reaching <see cref="ITowerUpgradeService" />.
///     Complements <c>TowerConstructAndHealServiceTests</c> (which drives <see cref="TowerUpgradeService" />'s own
///     <c>ConstructAsync</c>/<c>HealAsync</c> directly and exhaustively covers every validation gate) by proving
///     only the DISPATCH itself: that <see cref="UseInventoryItemService.ResolveAsync" /> now routes item 665/667
///     requests to that service -- via the item resolved from the addressed inventory slot, not a
///     directly-supplied <see cref="ItemStack" /> -- rather than silently falling through, and that the request's
///     own <c>value</c> field is threaded through unmodified as item 665's constructType.
/// </summary>
public class UseInventoryItemTowerServiceTests
{
    private const short TowerZoneNumber = 2;
    private const int TowerIndex = 0;
    private const int ConstructItemId = 665;
    private const int HealItemId = 667;
    private const int UnrelatedItemId = 999001;
    private const int Level1GuardianMonsterId = 589;
    private const byte Page = ContainerMatrix.InventoryPage0;
    private const byte Slot = 0;
    private const byte SpecialUseSort = 3;
    private const int AccountId = 1;
    private const int CharacterId = 10;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, TowerWarState TowerWar, UseInventoryItemService Service) SetUp(
            byte tribe = 0, short mapId = TowerZoneNumber)
    {
        var towerWar = new TowerWarState();
        var zone = ZoneTestKit.CreateZone(mapId, towerWar: towerWar);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(AccountId, CharacterId);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(CharacterId, ZoneTestKit.EnterData(session, mapId, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(CharacterId, out var state));
        state!.TribeRole = 1; // Force Leader -- one of the two accepted tower-build leadership roles

        var characters = new FakeCharacterRepository();
        var itemsById = new Dictionary<int, ItemDefinition>
        {
            [ConstructItemId] = new(WorldDataTestRows.Item(ConstructItemId) with { Sort = SpecialUseSort }, []),
            [HealItemId] = new(WorldDataTestRows.Item(HealItemId) with { Sort = SpecialUseSort }, []),
            [UnrelatedItemId] = new(WorldDataTestRows.Item(UnrelatedItemId) with { Sort = SpecialUseSort }, [])
        }.ToFrozenDictionary();
        var worldData = ZoneTestKit.EmptyWorldData(itemsById);

        var towerUpgrade = new TowerUpgradeService(towerWar, characters, NullLogger<TowerUpgradeService>.Instance);
        var service = new UseInventoryItemService(characters, new FakeGuildRepository(), new FakeCashRepository(),
            new FakeOfflineShopRepository(), new FakeEventLogRepository(), new FakeProxyShopExpirationRelayQueue(),
            Options.Create(new GameServerOptions()), worldData, NullLogger<UseInventoryItemService>.Instance,
            towerUpgrade);

        return (session, pipe, zone, state, characters, towerWar, service);
    }

    private static void SeedItem(Zone zone, int itemId)
    {
        var slots = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(Slot, new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(Page, slots));
        zone.PostInventoryCommand(new InventoryZoneCommand(CharacterId, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static MonsterEntity SpawnGuardian(Zone zone, bool damaged, int towerIndex = TowerIndex)
    {
        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        var template = WorldDataTestRows.Monster(Level1GuardianMonsterId) with { Life = 5000 };
        var guardian = MonsterEntity.Create(guardianIndex, 500u, template, guardianIndex, -1276f, -5f, 1826f, 300f);
        if (damaged)
            guardian.TakeDamage(1000, out _); // 5000 -> 4000, so it is below MaxLife

        zone.SpawnMonster(guardian);
        return guardian;
    }

    private static void PlaceAtGuardian(PlayerRuntimeState state)
    {
        state.PosX = -1276f;
        state.PosY = -5f;
        state.PosZ = 1826f;
    }

    private static async Task<UseInventoryItemResponse> RunToCompletionAsync(
        ValueTask<UseInventoryItemResponse> pending, Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("UseInventoryItemService task never completed.");
        }

        return await task;
    }

    /// <summary>
    ///     Regression guard for the fix itself: before this session, item 665 addressed via op23 had no
    ///     dispatch branch in <see cref="UseInventoryItemService.ResolveAsync" /> at all, so this request would
    ///     have fallen through to the generic Result=1 <c>Fail</c> with the tower war state never touched. It now
    ///     routes to <see cref="ITowerUpgradeService.ConstructAsync" />, and the request's own <c>value</c> field
    ///     (1=Silver/2=CP/3=EXP) is threaded through unmodified as the constructType -- confirmed against all
    ///     three legal values, matching <c>CZ_USE_INVENTORY_ITEM_SEND</c>'s single <c>tValue</c> field
    ///     (Server/Header/Protocol/CLIENT.h:243-248; no separate tValue01/tValue1 exists on this packet).
    /// </summary>
    [Theory]
    [InlineData(1)] // Silver Tower
    [InlineData(2)] // CP Tower
    [InlineData(3)] // EXP Tower
    public async Task ConstructItem_RoutesToTowerUpgradeService_UsesRequestValueAsConstructTypeUnmodified(int value)
    {
        var (session, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, Page, Slot, value, CancellationToken.None),
            zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result); // success -- was previously an unreachable code path for this item id
        Assert.Equal(value, towerWar.GetPendingConstructKind(TowerIndex));
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.ItemId == ConstructItemId);
    }

    /// <summary>
    ///     Same regression guard as above, for item 667: routes to <see cref="ITowerUpgradeService.HealAsync" />
    ///     and arms the tick-side heal, rather than falling through to the generic failure. The request's
    ///     <c>value</c> field is deliberately passed as an arbitrary, meaningless number here (999) -- item 667's
    ///     heal has no per-request parameter and <see cref="ITowerUpgradeService.HealAsync" />'s own signature has
    ///     no <c>value</c>/constructType parameter to (mis)route it through, so this also structurally confirms
    ///     the wiring never conflates the two items' request-value semantics.
    /// </summary>
    [Fact]
    public async Task HealItem_RoutesToTowerUpgradeService_ConsumesItemAndArmsTheTickSideHeal()
    {
        var (session, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, HealItemId);
        SpawnGuardian(zone, damaged: true);
        PlaceAtGuardian(state);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, Page, Slot, 999, CancellationToken.None),
            zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.True(towerWar.IsGuardianHealPending(TowerIndex));
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.ItemId == HealItemId);
    }

    /// <summary>
    ///     Sanity/regression guard the other way: adding the two new item-665/667 dispatch branches must not
    ///     widen matching to any other item id -- an unrelated item (registered in the catalog, Sort=3 like the
    ///     two tower items, but neither id) still falls through to the generic Result=1 failure with the tower
    ///     war state completely untouched.
    /// </summary>
    [Fact]
    public async Task UnrelatedItemId_StillFallsThroughToGenericFailure_TowerStateNeverTouched()
    {
        var (_, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, UnrelatedItemId);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, Page, Slot, 1, CancellationToken.None), zone);

        Assert.Equal(1, response.Result);
        Assert.Equal(0, towerWar.GetPendingConstructKind(TowerIndex));
        Assert.False(towerWar.IsGuardianHealPending(TowerIndex));
        Assert.Null(characters.LastReplacedContainer);
    }
}
