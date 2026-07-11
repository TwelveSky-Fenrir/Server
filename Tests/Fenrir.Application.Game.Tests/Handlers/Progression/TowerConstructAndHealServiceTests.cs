using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Progression;

/// <summary>
///     A11 -- drives the real <see cref="TowerUpgradeService" /> item-665 construct (<c>ConstructAsync</c>) and
///     item-667 heal (<c>HealAsync</c>) paths over a real <see cref="Zone" />, same tick-while-pending pattern as
///     <see cref="TowerUpgradeServiceTests" />. Zone 2 -&gt; <see cref="TowerZoneIndexTable" /> tower index 0,
///     owning tribe 0, guardian stand point (-1276, -5, 1826).
/// </summary>
public class TowerConstructAndHealServiceTests
{
    private const short TowerZoneNumber = 2;
    private const int TowerIndex = 0;
    private const int ConstructItemId = 665;
    private const int HealItemId = 667;
    private const int Level1GuardianMonsterId = 589;
    private const byte Page = ContainerMatrix.InventoryPage0;
    private const byte Slot = 0;

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, TowerWarState TowerWar, TowerUpgradeService Service) SetUp(
            byte tribe = 0, short mapId = TowerZoneNumber)
    {
        var towerWar = new TowerWarState();
        var zone = ZoneTestKit.CreateZone(mapId, towerWar: towerWar);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, mapId, tribe: tribe)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.TribeRole = 1; // Force Leader -- one of the two accepted tower-build leadership roles

        var characters = new FakeCharacterRepository();
        var service = new TowerUpgradeService(towerWar, characters, NullLogger<TowerUpgradeService>.Instance);
        return (session, pipe, zone, state, characters, towerWar, service);
    }

    private static void SeedItem(Zone zone, int itemId)
    {
        var slots = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(Slot, new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1));
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(Page, slots));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static ItemStack Item(int itemId)
    {
        return new ItemStack(itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1);
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

    private static async Task<UseInventoryItemResponse> RunAsync(ValueTask<UseInventoryItemResponse> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("Tower construct/heal task never completed.");
        }

        return await task;
    }

    // ---- item-665 construct -----------------------------------------------------------------------------------

    [Fact]
    public async Task Construct_ValidLeadershipOnOwnIdleTower_ConsumesItem_AndArmsConstruction()
    {
        var (session, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, result.Result); // success
        Assert.Equal(1, towerWar.GetPendingConstructKind(TowerIndex)); // Silver tower armed
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.ItemId == ConstructItemId);
    }

    [Fact]
    public async Task Construct_RegularMember_Rejected_ItemRetained_TowerUntouched()
    {
        var (_, _, zone, state, characters, towerWar, service) = SetUp();
        state.TribeRole = 0; // not a Force Leader/Assistant
        SeedItem(zone, ConstructItemId);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result); // soft failure, item retained
        Assert.Equal(0, towerWar.GetPendingConstructKind(TowerIndex));
        Assert.Null(characters.LastReplacedContainer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Construct_KindOutOfRange_Rejected(int kind)
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), kind, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
        Assert.Equal(0, towerWar.GetPendingConstructKind(TowerIndex));
    }

    [Fact]
    public async Task Construct_OnANonTowerZone_Rejected()
    {
        var (_, _, zone, state, _, _, service) = SetUp(mapId: 999);
        SeedItem(zone, ConstructItemId);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
    }

    [Fact]
    public async Task Construct_InAnotherTribesTowerZone_Rejected()
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp(tribe: 1); // zone 2's tower is owned by tribe 0
        SeedItem(zone, ConstructItemId);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
        Assert.Equal(0, towerWar.GetPendingConstructKind(TowerIndex));
    }

    [Fact]
    public async Task Construct_WhenAGuardianAlreadyStands_Rejected_TowerNotIdle()
    {
        var (_, _, zone, state, _, _, service) = SetUp();
        SeedItem(zone, ConstructItemId);
        SpawnGuardian(zone, damaged: false);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
    }

    [Fact]
    public async Task Construct_WhenTheTowerIsAlreadyBuilt_Rejected_TowerNotIdle()
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);
        towerWar.SetTowerState(TowerIndex, 201, true); // already an Active tower

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
    }

    [Fact]
    public async Task Construct_WhenMoreThanThreeOfTheKindExist_Rejected_ItemRetained()
    {
        var (_, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);

        // Four kind-1 towers already exist cluster-wide, none in tower 0's own {0,1,2} group -- the max-of-a-kind
        // gate fires before the group-adjacency gate.
        towerWar.SetTowerState(3, 201, true);
        towerWar.SetTowerState(4, 201, true);
        towerWar.SetTowerState(5, 201, true);
        towerWar.SetTowerState(6, 201, true);

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
        Assert.Null(characters.LastReplacedContainer); // never consumed
    }

    [Fact]
    public async Task Construct_WhenASiblingSlotAlreadyHoldsTheKind_Rejected_GroupAdjacency()
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp();
        SeedItem(zone, ConstructItemId);
        towerWar.SetTowerState(1, 201, true); // slot 1 shares tower 0's own 3-slot group

        var result = await RunAsync(
            service.ConstructAsync(10, zone, state, Page, Slot, Item(ConstructItemId), 1, CancellationToken.None),
            zone);

        Assert.Equal(1, result.Result);
        Assert.Equal(0, towerWar.GetPendingConstructKind(TowerIndex));
    }

    // ---- item-667 heal --------------------------------------------------------------------------------------

    [Fact]
    public async Task Heal_ValidWithinRangeOnADamagedGuardian_ConsumesItem_AndArmsTheTickSideHeal()
    {
        var (session, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, HealItemId);
        SpawnGuardian(zone, damaged: true);
        PlaceAtGuardian(state);

        var result = await RunAsync(
            service.HealAsync(10, zone, state, Page, Slot, Item(HealItemId), CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(0, result.Result);
        Assert.True(towerWar.IsGuardianHealPending(TowerIndex));
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.DoesNotContain(characters.LastReplacedContainer!.Value.Items, i => i.ItemId == HealItemId);
    }

    [Fact]
    public async Task Heal_WithNoLiveGuardian_Rejected_ItemRetained()
    {
        var (_, _, zone, state, characters, towerWar, service) = SetUp();
        SeedItem(zone, HealItemId);
        PlaceAtGuardian(state);

        var result = await RunAsync(
            service.HealAsync(10, zone, state, Page, Slot, Item(HealItemId), CancellationToken.None), zone);

        Assert.Equal(1, result.Result);
        Assert.False(towerWar.IsGuardianHealPending(TowerIndex));
        Assert.Null(characters.LastReplacedContainer);
    }

    [Fact]
    public async Task Heal_OutsideThe200UnitRange_Rejected()
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp();
        SeedItem(zone, HealItemId);
        SpawnGuardian(zone, damaged: true);
        // Player left at the default (100, 0, 100) spawn -- far from the (-1276, -5, 1826) tower.

        var result = await RunAsync(
            service.HealAsync(10, zone, state, Page, Slot, Item(HealItemId), CancellationToken.None), zone);

        Assert.Equal(1, result.Result);
        Assert.False(towerWar.IsGuardianHealPending(TowerIndex));
    }

    [Fact]
    public async Task Heal_OnAGuardianAlreadyAtFullLife_Rejected()
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp();
        SeedItem(zone, HealItemId);
        SpawnGuardian(zone, damaged: false); // full life
        PlaceAtGuardian(state);

        var result = await RunAsync(
            service.HealAsync(10, zone, state, Page, Slot, Item(HealItemId), CancellationToken.None), zone);

        Assert.Equal(1, result.Result);
        Assert.False(towerWar.IsGuardianHealPending(TowerIndex));
    }

    [Fact]
    public async Task Heal_OnANonTowerZone_Rejected()
    {
        var (_, _, zone, state, _, _, service) = SetUp(mapId: 999);
        SeedItem(zone, HealItemId);

        var result = await RunAsync(
            service.HealAsync(10, zone, state, Page, Slot, Item(HealItemId), CancellationToken.None), zone);

        Assert.Equal(1, result.Result);
    }

    [Fact]
    public async Task Heal_InAnotherTribesTowerZone_Rejected()
    {
        var (_, _, zone, state, _, towerWar, service) = SetUp(tribe: 1);
        SeedItem(zone, HealItemId);
        SpawnGuardian(zone, damaged: true);
        PlaceAtGuardian(state);

        var result = await RunAsync(
            service.HealAsync(10, zone, state, Page, Slot, Item(HealItemId), CancellationToken.None), zone);

        Assert.Equal(1, result.Result);
        Assert.False(towerWar.IsGuardianHealPending(TowerIndex));
    }
}
