using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

/// <summary>
///     Business logic extracted from <c>TowerUpgradeHandler</c> (CZ_CHUGSOUNG_WAR_UP_SEND, opcode 120).
///     Material consumption only arms the upgrade (<see cref="TowerWarState.BeginUpgrade" />); the actual
///     level change, guardian-monster respawn, and the later siege/destruction cycle all run off that
///     tower's own zone tick -- see <see cref="TowerGuardianSystem" />.
/// </summary>
public sealed class TowerUpgradeService(
    TowerWarState towerWar,
    ICharacterRepository characters,
    ILogger<TowerUpgradeService> logger) : ITowerUpgradeService
{
    private const int HerbItemId = 666;
    private const int BarItemId = 1073;

    public async ValueTask<TowerUpgradeResult> UpgradeAsync(int characterId, Zone zone, PlayerRuntimeState state,
        TowerUpgradeRequest packet, CancellationToken cancellationToken)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        var valid = towerIndex is >= 0 and < TowerWarState.TowerCount && towerWar.IsValid(towerIndex);
        var packedState = towerIndex is >= 0 and < TowerWarState.TowerCount ? towerWar.GetPackedState(towerIndex) : 0;

        var resolved = TowerUpgradeResolver.Validate(state.TribeRole, packet.Index, zone.MapId, state.Tribe,
            packet.Value01, packet.Value02, packedState, valid);

        if (resolved.Outcome != TowerUpgradeResolver.Outcome.Success)
            return new TowerUpgradeResult(TowerUpgradeOutcome.Aborted, 0, 0);

        var page0 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage0);
        var page1 = state.Inventory.GetContainer(ContainerMatrix.InventoryPage1);

        if (!TowerUpgradeResolver.TryFindMaterial(page0, page1, HerbItemId, out var herbPage, out var herbSlot) ||
            !TowerUpgradeResolver.TryFindMaterial(page0, page1, BarItemId, out var barPage, out var barSlot))
            return new TowerUpgradeResult(TowerUpgradeOutcome.Aborted, 0, 0);

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
            return new TowerUpgradeResult(TowerUpgradeOutcome.Aborted, 0, 0);
        }

        towerWar.BeginUpgrade(resolved.TowerIndex, resolved.NewPackedState, state.Tribe);

        logger.LogInformation(
            "Character {CharacterId} armed tower {TowerIndex} upgrade on map {MapId} for tribe {Tribe}",
            characterId, resolved.TowerIndex, zone.MapId, state.Tribe);

        var packedPage = herbPage + 10000 + barPage * 100;
        var packedIndex = herbSlot + 10000 + barSlot * 100;

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

        return new TowerUpgradeResult(TowerUpgradeOutcome.Success, packedPage, packedIndex);
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
