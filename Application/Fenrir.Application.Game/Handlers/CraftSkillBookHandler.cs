using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Crafting;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op30, CZ_MAKE_SKILL_SEND -- 4 fragment-to-skill-book recipes (S04_MyWork02.cpp:5868-6017). Recipes 0-2
///     are unconditional (no roll); recipe 3 (War God) additionally rolls the granted skill via
///     <see cref="SkillBookCraftResolver.ResolveWarGod" />.
/// </summary>
public sealed class CraftSkillBookHandler(
    ICharacterRepository characters,
    ILogger<CraftSkillBookHandler> logger)
    : IAsyncPacketHandler<CraftSkillBookRequest>
{
    public async ValueTask HandleAsync(CraftSkillBookRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        // Serializes the read/SQL/mirror sequence per character to close an item-duplication window.
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

    private async ValueTask ResolveAndApplyAsync(CraftSkillBookRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3) || !IsValidSlot(packet.Page4, packet.Index4))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);
        var material4 = state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            material4 is not { } m4)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = packet.Sort == SkillBookCraftCatalog.WarGodSort
            ? SkillBookCraftResolver.ResolveWarGod(m1.ItemId, m2.ItemId, m3.ItemId, m4.ItemId, state.PreviousTribe,
                SystemRandomSource.Instance)
            : SkillBookCraftResolver.ResolveFragments(packet.Sort, m1.ItemId, m2.ItemId, m3.ItemId, m4.ItemId);

        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newBook = m1 with
        {
            ItemId = resolved.ResultItemId, Quantity = 0, Enchant = 0, Combine = 0, Refine = 0, Socket = 0
        };

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newBook);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);
        working[(byte)packet.Page4] = working[(byte)packet.Page4].Remove((byte)packet.Index4);

        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        session.Send(new CraftSkillBookResponse
        {
            Result = 0, Value = [resolved.ResultItemId, 0, 0, 0, 0, newBook.Serial]
        });

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-skill-book mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
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
