using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

public sealed class TowerUpgradeService(
    TowerWarState towerWar,
    ICharacterRepository characters,
    ILogger<TowerUpgradeService> logger) : ITowerUpgradeService
{
    private const int HerbItemId = 666;
    private const int BarItemId = 1073;

    private const int TowerConstructItemId = 665;

    private const int TowerHealItemId = 667;

    private const float HealRangeSquared = 200f * 200f;

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

    public async ValueTask<UseInventoryItemResponse> ConstructAsync(int characterId, Zone zone,
        PlayerRuntimeState state, byte page, byte index, ItemStack item, int constructType,
        CancellationToken cancellationToken)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        if (towerIndex is < 0 or >= TowerWarState.TowerCount)
            return Fail(characterId, page, index, "not a tower zone");

        if (constructType is < 1 or > 3)
            return Fail(characterId, page, index, "construct kind out of range");

        if (state.TribeRole is not (1 or 2))
            return Fail(characterId, page, index, "not a tower-build leadership role");

        if (TowerZoneIndexTable.GetOwningTribe(zone.MapId) != state.Tribe)
            return Fail(characterId, page, index, "not the requester's own tribe zone");

        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        if (towerWar.GetPhase(towerIndex) != TowerSiegePhase.Dormant ||
            towerWar.GetPendingConstructKind(towerIndex) != 0 || zone.TryGetMonster(guardianIndex, out _))
            return Fail(characterId, page, index, "tower not idle");

        if (towerWar.CountTowersOfKind(constructType) > TowerWarState.MaxTowersPerKind)
            return Fail(characterId, page, index, "too many towers of this kind");

        if (towerWar.IsKindPresentInTribeGroup(towerIndex, constructType))
            return Fail(characterId, page, index, "kind already present in tribe group");

        if (!towerWar.BeginConstruction(towerIndex, constructType, state.Tribe))
            return Fail(characterId, page, index, "tower construction already claimed");

        var projected = ConsumeOne(state.Inventory.GetContainer(page), index);

        try
        {
            await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            towerWar.CancelConstruction(towerIndex);
            logger.LogWarning(ex,
                "Character {CharacterId} tower-construct item consumption failed on map {MapId}; construction rolled back",
                characterId, zone.MapId);
            return Fail(characterId, page, index, "item consumption failed");
        }

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped tower-construct material mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} armed tower {TowerIndex} construction (item {ItemId}, kind {Kind}) on map {MapId} for tribe {Tribe}",
            characterId, towerIndex, item.ItemId, constructType, zone.MapId, state.Tribe);

        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    public async ValueTask<UseInventoryItemResponse> HealAsync(int characterId, Zone zone, PlayerRuntimeState state,
        byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        if (towerIndex is < 0 or >= TowerWarState.TowerCount)
            return Fail(characterId, page, index, "not a tower zone");

        if (TowerZoneIndexTable.GetOwningTribe(zone.MapId) != state.Tribe)
            return Fail(characterId, page, index, "not the requester's own tribe zone");

        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        if (!zone.TryGetMonster(guardianIndex, out var guardian) || guardian is null)
            return Fail(characterId, page, index, "no live tower guardian");

        if (!WithinHealRange(state, zone.MapId))
            return Fail(characterId, page, index, "outside the tower heal range");

        if (guardian.Life >= guardian.MaxLife)
            return Fail(characterId, page, index, "tower guardian already at full life");

        var projected = ConsumeOne(state.Inventory.GetContainer(page), index);

        try
        {
            await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Character {CharacterId} tower-heal item consumption failed on map {MapId}", characterId, zone.MapId);
            return Fail(characterId, page, index, "item consumption failed");
        }

        towerWar.RequestGuardianHeal(towerIndex);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped tower-heal material mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} armed tower {TowerIndex} guardian heal (item {ItemId}) on map {MapId}",
            characterId, towerIndex, item.ItemId, zone.MapId);

        return new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    private static bool WithinHealRange(PlayerRuntimeState state, short mapId)
    {
        if (!TowerGuardianCatalog.TryGetGuardianLocation(mapId, out var tx, out var ty, out var tz))
            return false;

        var dx = state.PosX - tx;
        var dy = state.PosY - ty;
        var dz = state.PosZ - tz;
        return dx * dx + dy * dy + dz * dz <= HealRangeSquared;
    }

    private UseInventoryItemResponse Fail(int characterId, byte page, byte index, string reason)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Character {CharacterId} tower item-use rejected: {Reason} (slot {Page}:{Index})",
                characterId, reason, page, index);

        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 };
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
