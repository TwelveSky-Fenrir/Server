using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.ItemModification;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Crafting;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ItemModification;

/// <summary>
///     Business logic for op30, CZ_MAKE_SKILL_SEND -- extracted from <see cref="CraftSkillBookHandler" />, see
///     that handler's remarks. Recipe 3 (War God) additionally stands in for legacy's <c>MakeNotice</c> call
///     (Server/ts25zone/S04_MyWork02.cpp:6011) via <see cref="CenterRelayNoticeLog.LogNotableCraft" /> -- see
///     that type's own remarks for why this is a log line, not a client-facing broadcast.
/// </summary>
public sealed class CraftSkillBookService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<CraftSkillBookService> logger)
    : ICraftSkillBookService
{
    public async ValueTask<CraftSkillBookResult> ResolveAsync(CraftSkillBookRequest packet, Zone zone,
        PlayerRuntimeState state, int characterId, CancellationToken cancellationToken)
    {
        if (!IsValidSlot(packet.Page1, packet.Index1) || !IsValidSlot(packet.Page2, packet.Index2) ||
            !IsValidSlot(packet.Page3, packet.Index3) || !IsValidSlot(packet.Page4, packet.Index4))
        {
            logger.LogDebug("Character {CharacterId} craft-skill-book rejected: invalid slot(s)", characterId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        var material1 = state.Inventory.GetSlot((byte)packet.Page1, (byte)packet.Index1);
        var material2 = state.Inventory.GetSlot((byte)packet.Page2, (byte)packet.Index2);
        var material3 = state.Inventory.GetSlot((byte)packet.Page3, (byte)packet.Index3);
        var material4 = state.Inventory.GetSlot((byte)packet.Page4, (byte)packet.Index4);

        if (material1 is not { } m1 || material2 is not { } m2 || material3 is not { } m3 ||
            material4 is not { } m4)
        {
            logger.LogDebug(
                "Character {CharacterId} craft-skill-book rejected: one or more material slots empty", characterId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
        }

        var resolved = packet.Sort == SkillBookCraftCatalog.WarGodSort
            ? SkillBookCraftResolver.ResolveWarGod(m1.ItemId, m2.ItemId, m3.ItemId, m4.ItemId, state.PreviousTribe,
                SystemRandomSource.Instance)
            : SkillBookCraftResolver.ResolveFragments(packet.Sort, m1.ItemId, m2.ItemId, m3.ItemId, m4.ItemId);

        if (!resolved.Succeeded)
        {
            logger.LogInformation(
                "Character {CharacterId} craft-skill-book rejected by resolver (sort {Sort}, materials {M1}/{M2}/{M3}/{M4})",
                characterId, packet.Sort, m1.ItemId, m2.ItemId, m3.ItemId, m4.ItemId);
            return new CraftSkillBookResult(CraftSkillBookOutcome.Rejected, 0, 0);
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

        var containers = pages.Select(page => new InventoryContainerSnapshot(page, working[page]))
            .ToImmutableArray();
        if (!await zone.PostInventoryCommandAndWaitAsync(new InventoryZoneCommand(characterId, containers, null),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} inventory inbox full: dropped craft-skill-book mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} craft-skill-book applied: result item {ResultItemId}", characterId,
            resolved.ResultItemId);

        // Recipe 3 (War God) is the only one of the four that calls MakeNotice -- Server/ts25zone/
        // S04_MyWork02.cpp:6011. Recipes 0-2 never call it, so they get no log here.
        if (packet.Sort == SkillBookCraftCatalog.WarGodSort)
            CenterRelayNoticeLog.LogNotableCraft(logger, worldData, state.Tribe, state.Name, resolved.ResultItemId,
                "craft-skill-book (War God)");

        return new CraftSkillBookResult(CraftSkillBookOutcome.Applied, resolved.ResultItemId, newBook.Serial);
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
