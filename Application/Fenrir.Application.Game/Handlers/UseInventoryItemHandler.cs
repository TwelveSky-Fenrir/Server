using System.Collections.Immutable;
using Fenrir.Application.Game.Consumables;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Data.Guilds;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op23, CZ_USE_INVENTORY_ITEM_SEND -- three families out of the ~6300-line legacy switch are modeled:
///     the Bottle family (iSort==26, S04_MyWork03.cpp:2448, via <see cref="BottleResolver.ResolveAcquire" />)
///     and two members of the iSort==3 grab-bag of "right-click, single-purpose" items -- world.Items itself
///     shows iSort==3 covers wildly different behaviors (Guild Emblem register, Guild Boss Box loot roll,
///     Guild Scroll recharge, Faction Transfer Scroll) per specific ItemId, not per sort, so
///     <see cref="GuildScrollBuffMinutes" />/<see cref="IsTribeTransferScroll" /> dispatch on the item id
///     itself. Every other iSort/iIndex family (mounts, skills, costumes, cash-shop timers, the rest of the
///     iSort==3 bucket...) still replies with a clean Result=1 failure -- out of scope for this pass (see the
///     recon report's own Batch A conclusion).
/// </summary>
/// <remarks>
///     Not modeled: the per-tick anti-flood throttle (mTickForUseInventoryItem) and the page-1
///     storage-extension gate (aInventoryDate, no <see cref="PlayerRuntimeState" /> field exists yet) -- both
///     orthogonal to which item family is used, and neither has an acquisition/observation path through any
///     opcode implemented so far.
/// </remarks>
public sealed class UseInventoryItemHandler(
    ICharacterRepository characters,
    IGuildRepository guilds,
    WorldDataCache worldData,
    ILogger<UseInventoryItemHandler> logger)
    : IAsyncPacketHandler<UseInventoryItemRequest>
{
    private const byte BottleSort = 26;

    public async ValueTask HandleAsync(UseInventoryItemRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        var page = packet.Page;
        var index = packet.Index;

        if (page is not (ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1) ||
            !ContainerMatrix.IsValidSlot((byte)page, index))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await ResolveAndApplyAsync(session, zone, state, characterId, (byte)page, (byte)index,
                cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask ResolveAndApplyAsync(IPacketSession session, Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, CancellationToken cancellationToken)
    {
        var itemStack = state.Inventory.GetSlot(page, index);
        if (itemStack is not { } item || !worldData.ItemsById.TryGetValue(item.ItemId, out var itemDefinition))
        {
            session.Send(Fail(page, index));
            return;
        }

        if (itemDefinition.Item.Sort == BottleSort)
        {
            await ResolveBottleAsync(session, zone, state, characterId, page, index, item, cancellationToken);
            return;
        }

        if (GuildScrollBuffMinutes(item.ItemId) is { } minutes)
        {
            await ResolveGuildScrollAsync(session, zone, state, characterId, page, index, item, minutes,
                cancellationToken);
            return;
        }

        if (IsTribeTransferScroll(item.ItemId))
        {
            await ResolveTribeTransferScrollAsync(session, zone, state, characterId, page, index, item,
                cancellationToken);
            return;
        }

        session.Send(Fail(page, index));
    }

    private async ValueTask ResolveBottleAsync(IPacketSession session, Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var resolved = BottleResolver.ResolveAcquire(state.BottleSlots, item.ItemId);
        if (resolved.Outcome == BottleResolver.AcquireOutcome.Rejected)
        {
            session.Send(Fail(page, index));
            return;
        }

        var projected = state.Inventory.GetContainer(page).Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        session.Send(new UseInventoryItemResponse
        {
            Result = 0, Page = page, Index = index, Value = resolved.SlotIndex, Value2 = 0
        });

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
    private async ValueTask ResolveGuildScrollAsync(IPacketSession session, Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, int minutes, CancellationToken cancellationToken)
    {
        if (state.GuildId is not { } guildId)
        {
            session.Send(Fail(page, index));
            return;
        }

        var guild = await guilds.GetByIdAsync(guildId, cancellationToken);
        if (guild is null)
        {
            session.Send(Fail(page, index));
            return;
        }

        try
        {
            await guilds.SetBuffAsync(guildId, guild.BuffType, guild.BuffState, guild.BuffTime + minutes,
                guild.BuffTimeForDiff, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex,
                "Character {CharacterId} guild scroll recharge failed for guild {GuildId}", characterId, guildId);
            session.Send(Fail(page, index));
            return;
        }

        await ConsumeAndMirrorAsync(session, zone, state, characterId, page, index, item, cancellationToken);
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
    private async ValueTask ResolveTribeTransferScrollAsync(IPacketSession session, Zone zone,
        PlayerRuntimeState state, int characterId, byte page, byte index, ItemStack item,
        CancellationToken cancellationToken)
    {
        await characters.GrantTribeTransferPermitAsync(characterId, 1, cancellationToken);

        await ConsumeAndMirrorAsync(session, zone, state, characterId, page, index, item, cancellationToken);
    }

    /// <summary>
    ///     Shared consume/persist/reply tail for every family beyond Bottle: decrements one unit off the used
    ///     stack (removing the slot outright once it hits zero, unlike the Bottle branch which always removes
    ///     the whole slot regardless of quantity), persists the container, and mirrors it onto this zone's own
    ///     cache. Result=0/Value=0/Value2=0 -- none of these families carry a documented per-family payload.
    /// </summary>
    private async ValueTask ConsumeAndMirrorAsync(IPacketSession session, Zone zone, PlayerRuntimeState state,
        int characterId, byte page, byte index, ItemStack item, CancellationToken cancellationToken)
    {
        var remaining = item.Quantity - 1;
        var container = state.Inventory.GetContainer(page);
        var projected = remaining > 0
            ? container.SetItem(index, item with { Quantity = remaining })
            : container.Remove(index);

        await characters.ReplaceContainerAsync(characterId, page, ToTvps(projected), cancellationToken);

        session.Send(new UseInventoryItemResponse { Result = 0, Page = page, Index = index, Value = 0, Value2 = 0 });

        var containers = ImmutableArray.Create(new InventoryContainerSnapshot(page, projected));

        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped use-inventory-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);
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
