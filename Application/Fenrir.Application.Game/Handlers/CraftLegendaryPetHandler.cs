using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Crafting;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op131, CZ_MAKE_ITEM2_SEND -- only tSort==2 is reachable, an early <c>if (tSort != 2) Quit()</c> guard
///     makes tSort 0/1/3 dead code (S04_MyWork02.cpp:14902-14906). Upgrades an already-Legendary pet (item
///     world.Items.Sort 31/32) into a further-evolved Legendary/Guardian pet for 10,000 CP + 2 catalyst stones.
///     The server-wide "notable craft" announcement (<c>MakeNotice</c>) is not reproduced -- see
///     <see cref="CraftPetHandler" />'s own remarks.
/// </summary>
public sealed class CraftLegendaryPetHandler(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<CraftLegendaryPetHandler> logger)
    : IAsyncPacketHandler<CraftLegendaryPetRequest>
{
    public async ValueTask HandleAsync(CraftLegendaryPetRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item/CP-duplication window.
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

    private async ValueTask ResolveAndApplyAsync(CraftLegendaryPetRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (packet.Sort != LegendaryPetCraftCatalog.Sort ||
            !IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            !worldData.ItemsById.TryGetValue(m1.ItemId, out var material1Definition))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = LegendaryPetCraftResolver.Resolve(material1Definition.Item.Sort, m2.ItemId, m3.ItemId,
            state.ContributionPoints, SystemRandomSource.Instance);

        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newPet = m1 with
        {
            ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newPet);

        var remainingMaterial2Quantity = m2.Quantity - 1;
        working[(byte)packet.Page2] = remainingMaterial2Quantity > 0
            ? working[(byte)packet.Page2]
                .SetItem((byte)packet.Index2, m2 with { Quantity = remainingMaterial2Quantity })
            : working[(byte)packet.Page2].Remove((byte)packet.Index2);

        var remainingMaterial3Quantity = m3.Quantity - 1;
        working[(byte)packet.Page3] = remainingMaterial3Quantity > 0
            ? working[(byte)packet.Page3]
                .SetItem((byte)packet.Index3, m3 with { Quantity = remainingMaterial3Quantity })
            : working[(byte)packet.Page3].Remove((byte)packet.Index3);

        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        session.Send(new CraftLegendaryPetResponse
        {
            Result = LegendaryPetCraftCatalog.WireResult,
            Value = [resolved.ResultItemId, 0, 0, 0, 0, newPet.Serial],
            Padding = 0
        });

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-legendary-pet inventory mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(characterId,
                    state.ContributionPoints - LegendaryPetCraftCatalog.ContributionPointCost), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped CP mirror for character {CharacterId} after craft-legendary-pet",
                zone.MapId, characterId);
    }

    private static void EnsureContainer(Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working,
        PlayerRuntimeState state, byte page)
    {
        if (!working.ContainsKey(page))
            working[page] = state.Inventory.GetContainer(page);
    }

    private static bool IsValidSlot(int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index);
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
