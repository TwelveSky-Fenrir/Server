using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.GenericAction;
using Fenrir.Application.Game.Abstractions.Hotkeys;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Hotkeys;

public sealed class HotkeyActionService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<HotkeyActionService> logger)
    : IHotkeyActionService
{
    private const byte HotkeyBindableItemSort = 2;

    private const int ExcludedPotionType1A = 7;

    private const int ExcludedPotionType1B = 8;

    public async ValueTask<GenericActionResult> BindSkillAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int skillSlotIndex, int requestedGrade, int destinationPage, int destinationIndex,
        CancellationToken cancellationToken)
    {
        var destination = ResolveHotkeySlotOrEmpty(state, destinationPage, destinationIndex);

        var resolved = HotkeyActionResolver.ResolveBindSkill(destination, destinationPage, destinationIndex,
            skillSlotIndex, requestedGrade, state.LearnedSkills);

        if (!resolved.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} hotkey-bind-skill disconnect: resolver rejected ({Failure})",
                characterId, resolved.Failure);
            return GenericActionResult.Aborted;
        }

        await PersistAndMirrorSingleSlotAsync(zone, characterId, (byte)destinationPage, (byte)destinationIndex,
            resolved.NewDestination, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} hotkey-bind-skill applied: skill slot {SkillSlotIndex} -> hotkey {DestPage}:{DestIndex}",
            characterId, skillSlotIndex, destinationPage, destinationIndex);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> BindEmoticonAsync(Zone zone, PlayerRuntimeState state,
        int characterId, int emoticonCode, int destinationPage, int destinationIndex,
        CancellationToken cancellationToken)
    {
        var destination = ResolveHotkeySlotOrEmpty(state, destinationPage, destinationIndex);

        var resolved = HotkeyActionResolver.ResolveBindEmoticon(destination, destinationPage, destinationIndex,
            emoticonCode);

        if (!resolved.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} hotkey-bind-emoticon disconnect: resolver rejected ({Failure})",
                characterId, resolved.Failure);
            return GenericActionResult.Aborted;
        }

        await PersistAndMirrorSingleSlotAsync(zone, characterId, (byte)destinationPage, (byte)destinationIndex,
            resolved.NewDestination, cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} hotkey-bind-emoticon applied: code {EmoticonCode} -> hotkey {DestPage}:{DestIndex}",
            characterId, emoticonCode, destinationPage, destinationIndex);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> UnbindAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int page, int index, CancellationToken cancellationToken)
    {
        var slot = ResolveHotkeySlotOrEmpty(state, page, index);

        var resolved = HotkeyActionResolver.ResolveUnbind(slot, page, index);

        if (!resolved.Success)
        {
            logger.LogInformation("Character {CharacterId} hotkey-unbind disconnect: resolver rejected ({Failure})",
                characterId, resolved.Failure);
            return GenericActionResult.Aborted;
        }

        await PersistAndMirrorSingleSlotAsync(zone, characterId, (byte)page, (byte)index, HotkeySlot.Empty,
            cancellationToken);

        logger.LogInformation("Character {CharacterId} hotkey-unbind applied: {Page}:{Index}", characterId, page,
            index);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> BindItemAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, bool secondInventoryPageEntitlementActive, CancellationToken cancellationToken)
    {
        var sourcePage = move.Page1;
        var sourceIndex = move.Index1;
        var destinationPage = move.Page2;
        var destinationIndex = move.Index2;

        var destination = ResolveHotkeySlotOrEmpty(state, destinationPage, destinationIndex);
        var sourceItem = ResolveInventorySlotOrNull(state, sourcePage, sourceIndex);

        var sourceIsStackable = false;
        var sourceIsExcludedPotion = false;
        if (sourceItem is { } src && worldData.ItemsById.TryGetValue(src.ItemId, out var itemDefinition))
        {
            sourceIsStackable = itemDefinition.Item.Sort == HotkeyBindableItemSort;
            sourceIsExcludedPotion = itemDefinition.Item.PotionType1 is ExcludedPotionType1A or ExcludedPotionType1B;
        }

        var resolved = HotkeyActionResolver.ResolveBindItem(destination, destinationPage, destinationIndex,
            sourceItem, sourcePage, sourceIndex, move.Quantity1, sourceIsStackable, sourceIsExcludedPotion,
            secondInventoryPageEntitlementActive);

        if (!resolved.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} hotkey-bind-item disconnect: resolver rejected ({Failure})", characterId,
                resolved.Failure);
            return GenericActionResult.Aborted;
        }

        var sourceContainer = (byte)sourcePage;
        var projectedContainer = resolved.RemainingSourceQuantity > 0
            ? state.Inventory.GetContainer(sourceContainer)
                .SetItem((byte)sourceIndex, sourceItem!.Value with { Quantity = resolved.RemainingSourceQuantity })
            : state.Inventory.GetContainer(sourceContainer).Remove((byte)sourceIndex);

        await characters.UpsertHotkeySlotAsync(characterId, (byte)destinationPage, (byte)destinationIndex,
            resolved.NewDestination.Value1, resolved.NewDestination.Value2, (byte)resolved.NewDestination.Kind,
            cancellationToken);
        await characters.ReplaceContainerAsync(characterId, sourceContainer, ToTvps(projectedContainer),
            cancellationToken);

        var command = new HotkeyMoveZoneCommand(characterId,
            ImmutableArray.Create(new HotkeySlotWrite((byte)destinationPage, (byte)destinationIndex,
                resolved.NewDestination)),
            new InventoryContainerSnapshot(sourceContainer, projectedContainer));

        if (!await zone.PostHotkeyMoveCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} hotkey-move inbox full: dropped bind-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} hotkey-bind-item applied: inventory {SourceContainer}:{SourceIndex} -> hotkey {DestPage}:{DestIndex}",
            characterId, sourceContainer, sourceIndex, destinationPage, destinationIndex);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> WithdrawItemAsync(Zone zone, PlayerRuntimeState state,
        int characterId, DefaultPData move, bool secondInventoryPageEntitlementActive,
        CancellationToken cancellationToken)
    {
        var sourcePage = move.Page1;
        var sourceIndex = move.Index1;
        var destinationPage = move.Page2;
        var destinationIndex = move.Index2;

        var source = ResolveHotkeySlotOrEmpty(state, sourcePage, sourceIndex);
        var destinationItem = ResolveInventorySlotOrNull(state, destinationPage, destinationIndex);

        var resolved = HotkeyActionResolver.ResolveWithdrawItem(source, sourcePage, sourceIndex, move.Quantity1,
            destinationItem, destinationPage, destinationIndex, move.XPost2, move.YPost2,
            secondInventoryPageEntitlementActive);

        if (!resolved.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} hotkey-withdraw-item disconnect: resolver rejected ({Failure})",
                characterId, resolved.Failure);
            return GenericActionResult.Aborted;
        }

        var destinationContainer = (byte)destinationPage;
        var newDestinationStack = destinationItem is { } existingDestination
            ? existingDestination with
            {
                ItemId = resolved.NewDestinationItemId, Quantity = resolved.NewDestinationQuantity
            }
            : new ItemStack(resolved.NewDestinationItemId, resolved.NewDestinationQuantity, 0, 0, 0, 0, 0, 0, 0, 0,
                0);
        var projectedContainer = state.Inventory.GetContainer(destinationContainer)
            .SetItem((byte)destinationIndex, newDestinationStack);

        await characters.UpsertHotkeySlotAsync(characterId, (byte)sourcePage, (byte)sourceIndex,
            resolved.NewSource.Value1, resolved.NewSource.Value2, (byte)resolved.NewSource.Kind, cancellationToken);
        await characters.ReplaceContainerAsync(characterId, destinationContainer, ToTvps(projectedContainer),
            cancellationToken);

        var command = new HotkeyMoveZoneCommand(characterId,
            ImmutableArray.Create(new HotkeySlotWrite((byte)sourcePage, (byte)sourceIndex, resolved.NewSource)),
            new InventoryContainerSnapshot(destinationContainer, projectedContainer));

        if (!await zone.PostHotkeyMoveCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} hotkey-move inbox full: dropped withdraw-item mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} hotkey-withdraw-item applied: hotkey {SourcePage}:{SourceIndex} -> inventory {DestContainer}:{DestIndex}",
            characterId, sourcePage, sourceIndex, destinationContainer, destinationIndex);

        return GenericActionResult.Succeeded;
    }

    public async ValueTask<GenericActionResult> RearrangeAsync(Zone zone, PlayerRuntimeState state, int characterId,
        DefaultPData move, CancellationToken cancellationToken)
    {
        var sourcePage = move.Page1;
        var sourceIndex = move.Index1;
        var destinationPage = move.Page2;
        var destinationIndex = move.Index2;

        var source = ResolveHotkeySlotOrEmpty(state, sourcePage, sourceIndex);
        var destination = ResolveHotkeySlotOrEmpty(state, destinationPage, destinationIndex);

        var resolved = HotkeyActionResolver.ResolveRearrange(source, sourcePage, sourceIndex, destination,
            destinationPage, destinationIndex, move.Quantity1);

        if (!resolved.Success)
        {
            logger.LogInformation(
                "Character {CharacterId} hotkey-rearrange disconnect: resolver rejected ({Failure})", characterId,
                resolved.Failure);
            return GenericActionResult.Aborted;
        }

        await characters.UpsertHotkeySlotAsync(characterId, (byte)sourcePage, (byte)sourceIndex,
            resolved.NewSource.Value1, resolved.NewSource.Value2, (byte)resolved.NewSource.Kind, cancellationToken);
        await characters.UpsertHotkeySlotAsync(characterId, (byte)destinationPage, (byte)destinationIndex,
            resolved.NewDestination.Value1, resolved.NewDestination.Value2, (byte)resolved.NewDestination.Kind,
            cancellationToken);

        var command = new HotkeyMoveZoneCommand(characterId,
            ImmutableArray.Create(
                new HotkeySlotWrite((byte)sourcePage, (byte)sourceIndex, resolved.NewSource),
                new HotkeySlotWrite((byte)destinationPage, (byte)destinationIndex, resolved.NewDestination)),
            null);

        if (!await zone.PostHotkeyMoveCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} hotkey-move inbox full: dropped rearrange mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} hotkey-rearrange applied: {SourcePage}:{SourceIndex} -> {DestPage}:{DestIndex}",
            characterId, sourcePage, sourceIndex, destinationPage, destinationIndex);

        return GenericActionResult.Succeeded;
    }

    private async ValueTask PersistAndMirrorSingleSlotAsync(Zone zone, int characterId, byte page, byte index,
        HotkeySlot newSlot, CancellationToken cancellationToken)
    {
        await characters.UpsertHotkeySlotAsync(characterId, page, index, newSlot.Value1, newSlot.Value2,
            (byte)newSlot.Kind, cancellationToken);

        var command = new HotkeyMoveZoneCommand(characterId,
            ImmutableArray.Create(new HotkeySlotWrite(page, index, newSlot)), null);

        if (!await zone.PostHotkeyMoveCommandAndWaitAsync(command, cancellationToken))
            logger.LogError(
                "Zone {MapId} hotkey-move inbox full: dropped mirror for character {CharacterId} slot ({Page}:{Index}) -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId, page, index);
    }

    private static HotkeySlot ResolveHotkeySlotOrEmpty(PlayerRuntimeState state, int page, int index)
    {
        return HotkeyActionResolver.IsValidPage(page) && HotkeyActionResolver.IsValidIndex(index)
            ? state.GetHotkeySlot((byte)page, (byte)index)
            : HotkeySlot.Empty;
    }

    private static ItemStack? ResolveInventorySlotOrNull(PlayerRuntimeState state, int page, int index)
    {
        return page is ContainerMatrix.InventoryPage0 or ContainerMatrix.InventoryPage1 &&
               ContainerMatrix.IsValidSlot((byte)page, index)
            ? state.Inventory.GetSlot((byte)page, (byte)index)
            : null;
    }

    private static List<CharacterItemSlotTvp> ToTvps(ImmutableDictionary<byte, ItemStack> container)
    {
        var list = new List<CharacterItemSlotTvp>(container.Count);
        foreach (var (slot, stack) in container)
            list.Add(stack.ToTvp(slot));
        return list;
    }
}
