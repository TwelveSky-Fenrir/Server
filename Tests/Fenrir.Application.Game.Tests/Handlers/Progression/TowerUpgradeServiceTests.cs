using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Progression;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Handlers.Progression;

/// <summary>
///     Drives the real <see cref="TowerUpgradeService" /> (opcode 120) over a real <see cref="Zone" />, same
///     tick-while-pending pattern as <c>UseInventoryItemServiceTests</c>. Zone 2 -&gt; TowerZoneIndexTable
///     towerIndex 0, tribe 0's own block.
/// </summary>
public class TowerUpgradeServiceTests
{
    private const short TowerZoneNumber = 2;
    private const int TowerIndex = 0;
    private const int HerbItemId = 666;
    private const int BarItemId = 1073;

    private static async Task<TowerUpgradeResult> RunToCompletionAsync(ValueTask<TowerUpgradeResult> pending,
        Zone zone)
    {
        var task = pending.AsTask();
        var guard = 0;
        while (!task.IsCompleted)
        {
            zone.Tick(TimeSpan.FromMilliseconds(50));
            await Task.Yield();
            if (++guard > 100_000)
                throw new TimeoutException("TowerUpgradeService task never completed.");
        }

        return await task;
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe, Zone Zone, PlayerRuntimeState State,
        FakeCharacterRepository Characters, TowerWarState TowerWar) SetUp()
    {
        var zone = ZoneTestKit.CreateZone(TowerZoneNumber);
        var (session, pipe) = ZoneTestKit.CreateSession(1);
        session.MarkTicketConsumed(1, 10);
        session.CurrentZone = zone;
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, TowerZoneNumber, tribe: 0)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);
        Assert.True(zone.TryGetPlayer(10, out var state));
        state!.TribeRole = 1; // Force Leader -- only 1/2 may submit CZ_CHUGSOUNG_WAR_UP_SEND

        return (session, pipe, zone, state, new FakeCharacterRepository(), new TowerWarState());
    }

    private static void SeedHerbAndBar(Zone zone)
    {
        var slots = ImmutableDictionary<byte, ItemStack>.Empty
            .SetItem(0, new ItemStack(HerbItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1))
            .SetItem(1, new ItemStack(BarItemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2));
        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(ContainerMatrix.InventoryPage0, slots));
        zone.PostInventoryCommand(new InventoryZoneCommand(10, containers, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
    }

    private static TowerUpgradeService CreateService(FakeCharacterRepository characters, TowerWarState towerWar)
    {
        return new TowerUpgradeService(towerWar, characters, NullLogger<TowerUpgradeService>.Instance);
    }

    [Fact]
    public async Task ValidSubmission_ConsumesHerbAndBar_AndArmsTheTowerForItsNextLevel()
    {
        var (session, _, zone, state, characters, towerWar) = SetUp();
        SeedHerbAndBar(zone);
        towerWar.SetTowerState(TowerIndex, 201, true); // level 2, type 1 (Silver), armed for upgrade
        var service = CreateService(characters, towerWar);

        var result = await RunToCompletionAsync(
            service.UpgradeAsync(10, zone, state,
                new TowerUpgradeRequest { Index = TowerZoneNumber, Value01 = 1, Value02 = 2 },
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(TowerUpgradeOutcome.Success, result.Outcome);

        // Both materials removed from the same container (page 0) in one replace call.
        Assert.NotNull(characters.LastReplacedContainer);
        Assert.Equal(ContainerMatrix.InventoryPage0, characters.LastReplacedContainer!.Value.Container);
        Assert.DoesNotContain(characters.LastReplacedContainer.Value.Items, i => i.ItemId == HerbItemId);
        Assert.DoesNotContain(characters.LastReplacedContainer.Value.Items, i => i.ItemId == BarItemId);

        // Armed, not yet advanced -- TowerGuardianSystem's own tick promotes this to Active.
        Assert.False(towerWar.IsValid(TowerIndex));
        Assert.Equal(201, towerWar.GetPackedState(TowerIndex)); // unchanged until the guardian actually respawns
        Assert.Equal(TowerSiegePhase.Building, towerWar.GetPhase(TowerIndex));
        Assert.Equal(401, towerWar.GetPendingPackedStateForBuilding(TowerIndex));
    }

    [Fact]
    public async Task RegularMember_CannotSubmit_LeavesTheTowerUntouched()
    {
        var (session, _, zone, state, characters, towerWar) = SetUp();
        state.TribeRole = 0; // regular member, not Force Leader/Assistant
        SeedHerbAndBar(zone);
        towerWar.SetTowerState(TowerIndex, 201, true);
        var service = CreateService(characters, towerWar);

        var result = await RunToCompletionAsync(
            service.UpgradeAsync(10, zone, state,
                new TowerUpgradeRequest { Index = TowerZoneNumber, Value01 = 1, Value02 = 2 },
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(TowerUpgradeOutcome.Aborted, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
        Assert.True(towerWar.IsValid(TowerIndex)); // never armed -- rejected before any mutation
        Assert.Equal(TowerSiegePhase.Active, towerWar.GetPhase(TowerIndex));
    }

    [Fact]
    public async Task TowerNotArmed_AbortsWithoutTouchingInventory()
    {
        var (session, _, zone, state, characters, towerWar) = SetUp();
        SeedHerbAndBar(zone);
        towerWar.SetTowerState(TowerIndex, 201, false); // not currently valid for an upgrade
        var service = CreateService(characters, towerWar);

        var result = await RunToCompletionAsync(
            service.UpgradeAsync(10, zone, state,
                new TowerUpgradeRequest { Index = TowerZoneNumber, Value01 = 1, Value02 = 2 },
                CancellationToken.None), zone);

        Assert.Null(session.DisconnectReason);
        Assert.Equal(TowerUpgradeOutcome.Aborted, result.Outcome);
        Assert.Null(characters.LastReplacedContainer);
    }
}
