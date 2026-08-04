using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

public sealed class RerollItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<RerollItemService> logger)
    : IRerollItemService
{
    public async ValueTask<RerollItemResult> RerollAsync(RerollItemRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        var page1 = packet.Page1;
        var index1 = packet.Index1;

        if (page1 is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page1, index1))
        {
            logger.LogDebug("Character {CharacterId} reroll-item rejected: invalid slot ({Page1}:{Index1})",
                characterId, page1, index1);
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
        }

        if (!RentedInventoryPageGate.IsPageAccessible(page1, state.InventoryDate, GameDate.Today()))
        {
            logger.LogDebug(
                "Character {CharacterId} reroll-item rejected: rented inventory page expired (InventoryDate {InventoryDate})",
                characterId, state.InventoryDate);
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
        }

        var targetStack = state.Inventory.GetSlot((byte)page1, (byte)index1);
        if (targetStack is not { } target || !worldData.ItemsById.TryGetValue(target.ItemId, out var targetDefinition))
        {
            logger.LogDebug("Character {CharacterId} reroll-item rejected: target slot empty/unresolvable",
                characterId);
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
        }

        var catalog = worldData.ItemsById.Values.Select(d => d.Item);
        var resolved = RerollResolver.Resolve(targetDefinition.Item, state.PreviousTribe, catalog,
            SystemRandomSource.Instance);

        if (resolved.Outcome == RerollResolver.RerollOutcome.Rejected)
        {
            logger.LogInformation(
                "Character {CharacterId} reroll-item rejected by resolver (target {TargetItemId})", characterId,
                target.ItemId);
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
        }

        if (resolved.Outcome == RerollResolver.RerollOutcome.NoCandidate)
        {
            try
            {
                await characters.AdjustMoneyAsync(characterId, -resolved.Cost, 0, cancellationToken);
                await characters.AdjustMoneyAsync(characterId, resolved.Cost, 0, cancellationToken);
            }
            catch (Exception ex)
            {
                return AbortAfterUncertainPersistence(state, characterId, "reroll no-candidate funds check", ex);
            }

            logger.LogInformation(
                "Character {CharacterId} reroll-item found no candidate result item for target {TargetItemId}",
                characterId, target.ItemId);
            return new RerollItemResult(RerollItemOutcome.NoCandidate, resolved.Cost, [0, 0, 0, 0, 0, 0]);
        }

        var newStack = target with { ItemId = resolved.ResultItemId };
        var projected = state.Inventory.GetContainer((byte)page1).SetItem((byte)index1, newStack);

        try
        {
            await characters.AdjustMoneyAndReplaceContainerAsync(characterId, -resolved.Cost, 0, (byte)page1,
                ToTvps(projected), cancellationToken);
        }
        catch (Exception ex)
        {
            return AbortAfterUncertainPersistence(state, characterId, "reroll persistence", ex);
        }

        var packedValue = ItemValueCodec.Encode(target.Enchant, target.Combine, target.Refine, target.Socket);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        var inventoryResult = await zone.PostInventoryCommandAndWaitForResultAsync(
            new InventoryZoneCommand(characterId, containers, null), cancellationToken);
        if (inventoryResult.Kind != ZoneCommandResultKind.Applied)
            return AbortAfterDurableMutation(state, characterId, "reroll inventory", inventoryResult);

        zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

        logger.LogInformation(
            "Character {CharacterId} reroll-item applied: target {TargetItemId} -> {ResultItemId}, cost {Cost}",
            characterId, target.ItemId, resolved.ResultItemId, resolved.Cost);

        return new RerollItemResult(RerollItemOutcome.Applied, resolved.Cost,
            [resolved.ResultItemId, 0, 0, target.Quantity, packedValue, target.Serial]);
    }

    private RerollItemResult AbortAfterUncertainPersistence(PlayerRuntimeState state, int characterId,
        string mutation, Exception exception)
    {
        logger.LogError(exception,
            "Character {CharacterId} reroll-item {Mutation} failed after submission; durability is uncertain, disconnecting without success response",
            characterId, mutation);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new RerollItemResult(RerollItemOutcome.Disconnected, 0, [0, 0, 0, 0, 0, 0]);
    }

    private RerollItemResult AbortAfterDurableMutation(PlayerRuntimeState state, int characterId,
        string mutation, ZoneCommandResult result)
    {
        logger.LogError(
            "Character {CharacterId} reroll-item persisted but {Mutation} actor mutation was not acknowledged as applied ({Kind}: {Cause}); disconnecting without success response",
            characterId, mutation, result.Kind, result.Cause);
        ((IZoneSession)state.Session).Abort(DisconnectReason.Faulted);
        return new RerollItemResult(RerollItemOutcome.Disconnected, 0, [0, 0, 0, 0, 0, 0]);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
