using System.Collections.Immutable;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_CHUGSOUNG_WAR_UP_SEND (opcode 120). Every gate before the herb/bar lookup disconnects on
///     failure, matching the legacy's own Quit()-only error handling here. <c>FindInventoryItem</c>'s own
///     contract (S04_MyWork05.cpp:4867) never returns a hit with a -1 page/index, so the caller's
///     subsequent "page/index == -1" checks (S04_MyWork02.cpp:14396-14403) are dead code in the traced
///     source -- ZC_CHUGSOUNG_WAR_UP_RECV's Result=1/2 ("missing herb"/"missing bar") values are never
///     actually reachable; a missing herb or bar disconnects instead, same as every earlier gate.
///     <para>
///         Material consumption only arms the upgrade (<see cref="TowerWarState.BeginUpgrade" />); the actual
///         level change, guardian-monster respawn, and the later siege/destruction cycle all run off that
///         tower's own zone tick -- see <see cref="TowerGuardianSystem" />.
///     </para>
/// </summary>
public sealed class TowerUpgradeHandler(
    TowerWarState towerWar,
    ICharacterRepository characters,
    ILogger<TowerUpgradeHandler> logger) : IAsyncPacketHandler<TowerUpgradeRequest>
{
    private const int HerbItemId = 666;
    private const int BarItemId = 1073;

    public async ValueTask HandleAsync(TowerUpgradeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(TowerUpgradeRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        var valid = towerIndex is >= 0 and < TowerWarState.TowerCount && towerWar.IsValid(towerIndex);
        var packedState = towerIndex is >= 0 and < TowerWarState.TowerCount ? towerWar.GetPackedState(towerIndex) : 0;

        var resolved = TowerUpgradeResolver.Validate(state.TribeRole, packet.Index, zone.MapId, state.Tribe,
            packet.Value01, packet.Value02, packedState, valid);

        if (resolved.Outcome != TowerUpgradeResolver.Outcome.Success)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        if (!TowerUpgradeResolver.TryFindMaterial(page0, page1, HerbItemId, out var herbPage, out var herbSlot) ||
            !TowerUpgradeResolver.TryFindMaterial(page0, page1, BarItemId, out var barPage, out var barSlot))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var projectedHerb = ConsumeOne(herbPage == ContainerMatrix.InventoryPage0 ? page0 : page1, herbSlot);
        ImmutableDictionary<byte, ItemStack> projectedBar;

        if (herbPage == barPage)
        {
            projectedBar = ConsumeOne(projectedHerb, barSlot);
            projectedHerb = projectedBar;
        }
        else
        {
            projectedBar = ConsumeOne(barPage == ContainerMatrix.InventoryPage0 ? page0 : page1, barSlot);
        }

        try
        {
            if (herbPage == barPage)
                await characters.ReplaceContainerAsync(characterId, herbPage, ToTvps(projectedHerb),
                    cancellationToken);
            else
                await characters.ReplaceTwoContainersAsync(characterId, herbPage, ToTvps(projectedHerb), barPage,
                    ToTvps(projectedBar), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Character {CharacterId} tower-upgrade material consumption failed", characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        towerWar.BeginUpgrade(resolved.TowerIndex, resolved.NewPackedState, state.Tribe);

        var packedPage = herbPage + 10000 + barPage * 100;
        var packedIndex = herbSlot + 10000 + barSlot * 100;

        session.Send(new TowerUpgradeResponse
        {
            Result = 0, Page = [packedPage, 0], Index = [packedIndex, 0], Count = 1
        });

        var containers = herbPage == barPage
            ? ImmutableArray.Create(new InventoryContainerSnapshot(herbPage, projectedHerb))
            : ImmutableArray.Create(
                new InventoryContainerSnapshot(herbPage, projectedHerb),
                new InventoryContainerSnapshot(barPage, projectedBar));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped tower-upgrade material mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
    }

    private static ImmutableDictionary<byte, ItemStack> ConsumeOne(ImmutableDictionary<byte, ItemStack> container,
        byte slot)
    {
        var stack = container[slot];
        var remaining = stack.Quantity - 1;
        return remaining > 0 ? container.SetItem(slot, stack with { Quantity = remaining }) : container.Remove(slot);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
