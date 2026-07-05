using System.Collections.Immutable;
using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Crafting;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Characters;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op88, CZ_MAKE_PET_SEND -- 6 pet-fusion recipes (S04_MyWork02.cpp:12125-12501, LNW33+__GOD__ build). The
///     server-wide "notable craft" announcement (<c>MakeNotice</c> -&gt; Center broadcast) has no single-process
///     equivalent in Fenrir and is not reproduced here, matching the precedent set for other cross-server
///     notices (e.g. TribeBank's audit trail).
/// </summary>
public sealed class CraftPetHandler(
    ICharacterRepository characters,
    ILogger<CraftPetHandler> logger)
    : IAsyncPacketHandler<CraftPetRequest>
{
    public async ValueTask HandleAsync(CraftPetRequest packet, IPacketSession session,
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

    private async ValueTask ResolveAndApplyAsync(CraftPetRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        switch (packet.Sort)
        {
            case PetCraftRecipeCatalog.Recipe1Sort:
            case PetCraftRecipeCatalog.Recipe2Sort:
            case PetCraftRecipeCatalog.Recipe3Sort:
                await HandleFourSlotRecipeAsync(packet, session, zoneSession, zone, state, characterId,
                    cancellationToken);
                return;
            case PetCraftRecipeCatalog.Recipe4Sort:
            case PetCraftRecipeCatalog.Recipe5Sort:
            case PetCraftRecipeCatalog.Recipe6Sort:
                await HandleTwoSlotRecipeAsync(packet, session, zoneSession, zone, state, characterId,
                    cancellationToken);
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    /// <summary>Recipes 1-3: 3 fusion materials (page1-3) + 1 catalyst (page4, consumed 1 unit at a time).</summary>
    private async ValueTask HandleFourSlotRecipeAsync(CraftPetRequest packet, IPacketSession session,
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
        var catalyst = state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            catalyst is not { } cat)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = packet.Sort switch
        {
            PetCraftRecipeCatalog.Recipe1Sort => PetCraftResolver.ResolveRecipe1(m1, m2, m3, cat,
                SystemRandomSource.Instance),
            PetCraftRecipeCatalog.Recipe2Sort => PetCraftResolver.ResolveRecipe2(m1, m2, m3, cat,
                SystemRandomSource.Instance),
            _ => PetCraftResolver.ResolveRecipe3(m1, m2, m3, cat)
        };

        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newPet = m1 with
        {
            ItemId = resolved.ResultItemId, Quantity = resolved.ResultQuantity, Enchant = resolved.Enchant,
            Combine = resolved.Combine, Refine = resolved.Refine, Socket = resolved.Socket
        };

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);
        EnsureContainer(working, state, (byte)packet.Page3);
        EnsureContainer(working, state, (byte)packet.Page4);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newPet);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);
        working[(byte)packet.Page3] = working[(byte)packet.Page3].Remove((byte)packet.Index3);

        var remainingCatalystQuantity = cat.Quantity - 1;
        working[(byte)packet.Page4] = remainingCatalystQuantity > 0
            ? working[(byte)packet.Page4]
                .SetItem((byte)packet.Index4, cat with { Quantity = remainingCatalystQuantity })
            : working[(byte)packet.Page4].Remove((byte)packet.Index4);

        await PersistAndRespondAsync(session, zone, characterId, working, resolved, newPet, 10000,
            cancellationToken);
    }

    /// <summary>Recipes 4-6: exactly 2 materials (page1/page2), page2 always fully consumed (never decremented).</summary>
    private async ValueTask HandleTwoSlotRecipeAsync(CraftPetRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken cancellationToken)
    {
        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);

        if (material1 is not { } m1 || material2 is not { } m2)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var resolved = packet.Sort switch
        {
            PetCraftRecipeCatalog.Recipe4Sort => PetCraftResolver.ResolveRecipe4(m1, m2),
            PetCraftRecipeCatalog.Recipe5Sort => PetCraftResolver.ResolveRecipe5(m1, m2, SystemRandomSource.Instance),
            _ => PetCraftResolver.ResolveRecipe6(m1, m2, SystemRandomSource.Instance)
        };

        if (!resolved.Succeeded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newPet = m1 with
        {
            ItemId = resolved.ResultItemId, Quantity = resolved.ResultQuantity, Enchant = resolved.Enchant,
            Combine = resolved.Combine, Refine = resolved.Refine, Socket = resolved.Socket
        };

        var working = new Dictionary<byte, ImmutableDictionary<byte, ItemStack>>();
        EnsureContainer(working, state, (byte)packet.Page1);
        EnsureContainer(working, state, (byte)packet.Page2);

        working[(byte)packet.Page1] = working[(byte)packet.Page1].SetItem((byte)packet.Index1, newPet);
        working[(byte)packet.Page2] = working[(byte)packet.Page2].Remove((byte)packet.Index2);

        await PersistAndRespondAsync(session, zone, characterId, working, resolved, newPet, 0,
            cancellationToken);
    }

    private async ValueTask PersistAndRespondAsync(IPacketSession session, Zone zone, int characterId,
        Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working, PetCraftResolver.Result resolved,
        ItemStack newPet, int wireResult, CancellationToken cancellationToken)
    {
        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        session.Send(new CraftPetResponse
        {
            Result = wireResult, Value = [resolved.ResultItemId, 0, 0, resolved.ResultQuantity, 0, newPet.Serial]
        });

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-pet mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
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
