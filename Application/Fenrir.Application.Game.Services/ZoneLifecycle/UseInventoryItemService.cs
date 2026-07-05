using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class UseInventoryItemService(
    ICharacterRepository characters,
    IGuildRepository guilds,
    WorldDataCache worldData,
    ILogger<UseInventoryItemService> logger) : IUseInventoryItemService
{
    private const byte BottleSort = 26;

    public async ValueTask<UseInventoryItemResponse> ResolveAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition))
            return Fail(page, index);

        if (itemDefinition.Item.Sort == BottleSort)
            return await ResolveBottleAsync(zone, state, characterId, page, index, item, cancellationToken);

        if (GuildScrollBuffMinutes(item.ItemId) is { } minutes)
            return await ResolveGuildScrollAsync(zone, state, characterId, page, index, item, minutes,
                cancellationToken);

        if (IsTribeTransferScroll(item.ItemId))
            return await ResolveTribeTransferScrollAsync(zone, state, characterId, page, index, item,
                cancellationToken);

        return Fail(page, index);
    }

    private async ValueTask<UseInventoryItemResponse> ResolveBottleAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = BottleResolver.ResolveAcquire(state.BottleSlots, item.ItemId);
        if (resolved.Outcome == BottleResolver.AcquireOutcome.Rejected)
            return Fail(page, index);

        var projected = state.Inventory.GetContainer(page).Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var response = new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = resolved.SlotIndex, Value2 = 0
        };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item (bottle) mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (!zone.PostDrinkBottleCommand(new DrinkBottleZoneCommand(characterId, resolved.SlotIndex,
                resolved.RefilledCount, state.Life, item.ItemId)))
            logger.LogError(
                "Zone {MapId} bottle inbox full: dropped bottle-acquire mirror for character {CharacterId}",
                zone.MapId, characterId);

        return response;
    }

    /// <summary>
    ///     Guild Scroll items (world.Items 558/1211/8415) recharge game.Guilds.BuffTime by a fixed per-item
    ///     amount -- <c>GuildActionHandler</c>'s tSort=14 buff-type choice can only ever be exercised once
    ///     this reserve is non-zero (see its own remarks: "only ever recharged by tSort 15's guild scrolls").
    ///     Requires guild membership, matching every scroll's own item text ("Need to join/be in a guild to
    ///     use the item"); a non-member or an already-vanished guild gets the same clean Result=1 as any
    ///     other rejected use, not a disconnect. BuffType/BuffState/BuffTimeForDiff are carried through
    ///     unchanged -- this only tops up the time reserve, it never itself activates/changes a buff type.
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ResolveGuildScrollAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        if (state.GuildId is not { } guildId)
            return Fail(page, index);

        var guild = await guilds.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
            return Fail(page, index);

        try
        {
            await guilds.SetBuffAsync(guildId, guild.BuffType, guild.BuffState, guild.BuffTime + minutes,
                guild.BuffTimeForDiff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild scroll recharge failed for guild {GuildId}", characterId, guildId);
            return Fail(page, index);
        }

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Faction Transfer Scroll items (world.Items 8153/8154, "Transfer to any Faction. Right click to
    ///     use.") -- only the permission gate is modeled here: consuming the scroll credits one banked
    ///     transfer permit (game.Characters.TribeTransferPermitCount) via
    ///     <see cref="ICharacterRepository.GrantTribeTransferPermitAsync" />. This is the mirror image of
    ///     game.Characters.BloodCoin (which has a spend path but no known grant path) -- a grant path with no
    ///     spend path yet, because no legacy source available here documents the actual tribe-change
    ///     mechanic that would consume it. No tribe mutation is invented.
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ResolveTribeTransferScrollAsync(Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        await characters.GrantTribeTransferPermitAsync(characterId, 1, cancellationToken);

        return await ConsumeAndMirrorAsync(zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Shared consume/persist/reply tail for every family beyond Bottle: decrements one unit off the used
    ///     stack (removing the slot outright once it hits zero, unlike the Bottle branch which always removes
    ///     the whole slot regardless of quantity), persists the container, and mirrors it onto this zone's own
    ///     cache. Result=0/Value=0/Value2=0 -- none of these families carry a documented per-family payload.
    /// </summary>
    private async ValueTask<UseInventoryItemResponse> ConsumeAndMirrorAsync(Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var remaining = item.Quantity - 1;
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        var response = new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 };

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return response;
    }

    /// <summary>
    ///     world.Items.Description1 states each scroll's minutes ("Accumulate Guild Scroll time for N
    ///     minutes.") but no numeric catalog column encodes it separately -- a code constant per item id here,
    ///     the same posture as <see cref="BottleResolver.RefillCount" />.
    /// </summary>
    private static int? GuildScrollBuffMinutes(int itemId)
    {
        return itemId switch
        {
            558 => 30,
            1211 or 8415 => 60,
            _ => null
        };
    }

    private static bool IsTribeTransferScroll(int itemId)
    {
        return itemId is 8153 or 8154;
    }

    private static UseInventoryItemResponse Fail(byte page, byte index)
    {
        return new UseInventoryItemResponse { Result = 1, Page = page, Index = index, Value = 0, Value2 = 0 };
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
