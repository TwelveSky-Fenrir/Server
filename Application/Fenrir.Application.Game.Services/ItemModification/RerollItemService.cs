using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op26, CZ_EXCHANGE_ITEM_SEND -- extracted from <see cref="RerollItemHandler" />, see
///     that handler's remarks.
/// </summary>
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
            // Server/ts25zone/S04_MyWork02.cpp:3853-3858 -- legacy checks fund sufficiency once,
            // immediately after eligibility validation and before AddTribeBankInfo2's tribe-bank credit,
            // and that check gates BOTH the NoCandidate and Success outcomes identically: insufficient
            // money drops the connection regardless of which outcome the replacement-item lookup (already
            // resolved above, a pure/no-side-effect computation) produced. Fenrir has no in-memory Money
            // balance to compare against for a cheap, non-mutating check the way legacy does (Money lives
            // only in SQL, via ICharacterRepository) -- the Success path below already gets this gate for
            // free from AdjustMoneyAndReplaceContainerAsync's own throw-on-insufficient-balance contract,
            // but this NoCandidate path previously never touched money at all, so an insufficient-funds
            // character received a normal "no candidate" response instead of being dropped. Reproduced
            // here as an immediate debit-then-refund round trip through the same AdjustMoneyAsync
            // primitive the Success path relies on: the debit surfaces the identical SQL 50222
            // "insufficient balance" throw, and the matching refund keeps the net player-money effect at
            // zero on this path, preserving the cited NoCandidate asymmetry below (tribe bank credited,
            // player never net-debited).
            try
            {
                await characters.AdjustMoneyAsync(characterId, -resolved.Cost, 0, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Character {CharacterId} reroll-item funds check failed for no-candidate cost {Cost} (treated as insufficient funds)",
                    characterId, resolved.Cost);
                return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
            }

            await characters.AdjustMoneyAsync(characterId, resolved.Cost, 0, cancellationToken);

            // Server/ts25zone/S04_MyWork02.cpp:3853-3858,3921-3928 -- AddTribeBankInfo2 credits 1% of the
            // reroll cost to the tribe bank right after the legacy's own funds check, BEFORE the
            // replacement-item lookup that can come back empty -- a real, cited legacy asymmetry: the
            // tribe bank is credited here even though the player is never actually (net) debited on this
            // path (the only debit line that isn't unwound is reached exclusively on the success path
            // below). Preserved faithfully, not "fixed" -- see the mirrored credit on the success path
            // below.
            zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

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
            logger.LogWarning(ex,
                "Character {CharacterId} reroll-item AdjustMoneyAndReplaceContainerAsync failed (treated as insufficient funds)",
                characterId);
            return new RerollItemResult(RerollItemOutcome.Rejected, 0, [0, 0, 0, 0, 0, 0]);
        }

        // Same AddTribeBankInfo2 credit as the NoCandidate branch above (Server/ts25zone/
        // S04_MyWork02.cpp:3858), applied here -- after the debit has actually succeeded -- rather than
        // before the DB call, so a failed debit (e.g. a genuine insufficient-funds race) never credits the
        // tribe bank for money that was never actually taken. Legacy's own synchronous funds check made
        // that scenario impossible there; this ordering is the closest-fidelity equivalent under Fenrir's
        // async/DB-enforced funds check.
        zone.CreditNpcServiceTribeTax(state.Tribe, resolved.Cost);

        var packedValue = ItemValueCodec.Encode(target.Enchant, target.Combine, target.Refine, target.Socket);

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot((byte)page1, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped reroll mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} reroll-item applied: target {TargetItemId} -> {ResultItemId}, cost {Cost}",
            characterId, target.ItemId, resolved.ResultItemId, resolved.Cost);

        return new RerollItemResult(RerollItemOutcome.Applied, resolved.Cost,
            [resolved.ResultItemId, 0, 0, target.Quantity, packedValue, target.Serial]);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
