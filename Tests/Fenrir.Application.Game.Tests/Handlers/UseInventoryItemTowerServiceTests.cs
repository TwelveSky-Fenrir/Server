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
        state!.TribeRole = 1;

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
            guardian.TakeDamage(1000, out _);

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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ConstructItem_RoutesToTowerUpgradeService_UsesRequestValueAsConstructTypeUnmodified(int value)
    {
        var (session, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);

        var response = await RunToCompletionAsync(
            service.ResolveAsync(zone, state, CharacterId, AccountId, Page, Slot, value, CancellationToken.None),
            zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, response.Result);
        Assert.Equal(value, towerWar.GetPendingConstructKind(TowerIndex));
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.ItemId == ConstructItemId);
    }

    [Fact]
    public async Task HealItem_RoutesToTowerUpgradeService_ConsumesItemAndArmsTheTickSideHeal()
    {
        var (session, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, HealItemId);
        SpawnGuardian(zone, true);
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
