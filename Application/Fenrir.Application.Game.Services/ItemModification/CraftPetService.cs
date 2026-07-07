using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op88, CZ_MAKE_PET_SEND -- extracted from <see cref="CraftPetHandler" />, see that
///     handler's remarks.
/// </summary>
public sealed class CraftPetService(
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    ILogger<CraftPetService> logger)
    : ICraftPetService
{
    /// <summary>
    ///     game.EventLog.EventCode for the 3-material-plus-catalyst fusion recipes (Recipe1-3) -- scoped
    ///     independently within <see cref="EventLogCategory.ItemCreate" />, same "app-owned, per-class
    ///     numbering" posture as <see cref="CraftItemService" />'s own EventCode constants.
    /// </summary>
    private const short FourSlotRecipeEventCode = 1;

    /// <summary>game.EventLog.EventCode for the 2-material recipes (Recipe4-6).</summary>
    private const short TwoSlotRecipeEventCode = 2;

    /// <summary>Recipes 1-3: 3 fusion materials (page1-3) + 1 catalyst (page4, consumed 1 unit at a time).</summary>
    public async ValueTask<CraftPetResult> ResolveFourSlotRecipeAsync(CraftPetRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3) || !IsValidSlot(packet.Page4, packet.Index4))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);
        var catalyst = state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            catalyst is not { } cat)
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var resolved = packet.Sort switch
        {
            PetCraftRecipeCatalog.Recipe1Sort => PetCraftResolver.ResolveRecipe1(m1, m2, m3, cat,
                SystemRandomSource.Instance),
            PetCraftRecipeCatalog.Recipe2Sort => PetCraftResolver.ResolveRecipe2(m1, m2, m3, cat,
                SystemRandomSource.Instance),
            _ => PetCraftResolver.ResolveRecipe3(m1, m2, m3, cat)
        };

        if (!resolved.Succeeded)
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

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

        return await PersistAndBuildResultAsync(zone, characterId, accountId, FourSlotRecipeEventCode, working,
            resolved, newPet, 10000, cancellationToken);
    }

    /// <summary>Recipes 4-6: exactly 2 materials (page1/page2), page2 always fully consumed (never decremented).</summary>
    public async ValueTask<CraftPetResult> ResolveTwoSlotRecipeAsync(CraftPetRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, int accountId, CancellationToken cancellationToken)
    {
        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2))
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);

        if (material1 is not { } m1 || material2 is not { } m2)
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

        var resolved = packet.Sort switch
        {
            PetCraftRecipeCatalog.Recipe4Sort => PetCraftResolver.ResolveRecipe4(m1, m2),
            PetCraftRecipeCatalog.Recipe5Sort => PetCraftResolver.ResolveRecipe5(m1, m2, SystemRandomSource.Instance),
            _ => PetCraftResolver.ResolveRecipe6(m1, m2, SystemRandomSource.Instance)
        };

        if (!resolved.Succeeded)
            return new CraftPetResult(CraftPetOutcome.Rejected, 0, 0, 0, 0);

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

        return await PersistAndBuildResultAsync(zone, characterId, accountId, TwoSlotRecipeEventCode, working,
            resolved, newPet, 0, cancellationToken);
    }

    private async ValueTask<CraftPetResult> PersistAndBuildResultAsync(Zone zone, int characterId, int accountId,
        short eventCode, Dictionary<byte, ImmutableDictionary<byte, ItemStack>> working,
        PetCraftResolver.Result resolved, ItemStack newPet, int wireResult, CancellationToken cancellationToken)
    {
        var pages = working.Keys.ToArray();
        if (pages.Length == 1)
            await characters.ReplaceContainerAsync(characterId, pages[0], ToTvps(working[pages[0]]),
                cancellationToken);
        else
            await characters.ReplaceTwoContainersAsync(characterId, pages[0], ToTvps(working[pages[0]]), pages[1],
                ToTvps(working[pages[1]]), cancellationToken);

        // Logged only once the container replace(s) above have durably committed -- an ItemCreate row must
        // never assert a mint that the DB write didn't actually persist (same posture as CraftItemService's
        // own craft-family logging). ResultQuantity 0 is the "a single fresh pet unit" convention (see
        // PetCraftResolver's own remarks); the roll-miss "consolation" branches genuinely stack, so the logged
        // Quantity floors at 1 rather than asserting a false 0-unit mint, same convention CraftItemService's
        // MountFusion/WingTierReroll/WingFifthTier logging already uses.
        await eventLog.LogAsync(eventCode, EventLogCategory.ItemCreate, accountId, characterId, null, null, null,
            null, null, resolved.ResultItemId, Math.Max(resolved.ResultQuantity, 1), 1, null, cancellationToken);

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-pet mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        return new CraftPetResult(CraftPetOutcome.Applied, wireResult, resolved.ResultItemId,
            resolved.ResultQuantity, newPet.Serial);
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
