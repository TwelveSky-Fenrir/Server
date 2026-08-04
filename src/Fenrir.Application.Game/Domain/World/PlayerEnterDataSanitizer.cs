using Fenrir.Application.Game.Domain.Costumes;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.StellarCores;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.World;

public static class PlayerEnterDataSanitizer
{
    public static PlayerEnterDataSanitization Sanitize(
        IReadOnlyList<CharacterItemSlotDto> items,
        IReadOnlyList<CharacterHotkeyDto> hotkeys,
        IReadOnlyList<CharacterCostumeSlotDto> costumes,
        IReadOnlyList<CharacterStellarCoreSlotDto> stellarCores,
        IReadOnlyDictionary<int, ItemDefinition> itemsById,
        int today,
        int life,
        int mana)
    {
        var retainedItems = new List<CharacterItemSlotDto>(items.Count);
        var cleanedContainers = new SortedSet<byte>();

        foreach (var item in items)
        {
            var isKnownContainer = ContainerMatrix.TryGetMaxSlot(item.Container, out _);
            var isValid = isKnownContainer &&
                          ContainerMatrix.IsValidSlot(item.Container, item.Slot) &&
                          item.ItemId > 0 &&
                          itemsById.TryGetValue(item.ItemId, out var definition) &&
                          IsPersistedQuantityValid(definition.Item.Sort, item.Quantity) &&
                          !ItemExpirationCatalog.IsExpiredAtWorldEntry(item.ItemId, item.ExpireDate, today);

            if (isValid)
            {
                retainedItems.Add(item);
                continue;
            }

            if (isKnownContainer)
                cleanedContainers.Add(item.Container);
        }

        var sanitizedHotkeys = new List<CharacterHotkeyDto>(hotkeys.Count);
        var clearedHotkeys = new List<CharacterHotkeyDto>();

        foreach (var hotkey in hotkeys)
        {
            if (!HotkeyActionResolver.IsValidPage(hotkey.Page) || !HotkeyActionResolver.IsValidIndex(hotkey.KeyIndex))
                continue;

            var isItemBinding = hotkey.Value2 == (int)HotkeyBindingKind.Item;
            var isValidItemBinding = !isItemBinding ||
                                     (hotkey.Sort > 0 &&
                                      itemsById.TryGetValue(hotkey.Sort, out var definition) &&
                                      ItemQuantityPolicy.IsStackableSort(definition.Item.Sort) &&
                                      hotkey.Value1 is >= HotkeyActionResolver.MinItemQuantity and <=
                                          HotkeyActionResolver.MaxItemQuantity);
            var isKnownBindingKind = hotkey.Value2 is >= (int)HotkeyBindingKind.None and <=
                (int)HotkeyBindingKind.Item;

            if (isKnownBindingKind && isValidItemBinding)
            {
                sanitizedHotkeys.Add(hotkey);
                continue;
            }

            var cleared = new CharacterHotkeyDto(hotkey.Page, hotkey.KeyIndex, 0, 0, 0);
            sanitizedHotkeys.Add(cleared);
            clearedHotkeys.Add(cleared);
        }

        var sanitizedCostumes = new List<CharacterCostumeSlotDto>(costumes.Count);
        foreach (var costume in costumes)
            if (costume.Slot < CostumePersistenceCodec.SlotCount && costume.ItemId > 0 &&
                itemsById.ContainsKey(costume.ItemId))
                sanitizedCostumes.Add(costume);

        var sanitizedStellarCores = new List<CharacterStellarCoreSlotDto>(stellarCores.Count);
        foreach (var stellarCore in stellarCores)
            if (stellarCore.Slot < StellarCorePersistenceCodec.SlotCount && stellarCore.ItemId > 0 &&
                itemsById.ContainsKey(stellarCore.ItemId))
                sanitizedStellarCores.Add(stellarCore);

        return new PlayerEnterDataSanitization(
            retainedItems,
            [.. cleanedContainers],
            sanitizedHotkeys,
            clearedHotkeys,
            sanitizedCostumes,
            sanitizedCostumes.Count != costumes.Count,
            sanitizedStellarCores,
            sanitizedStellarCores.Count != stellarCores.Count,
            Math.Max(life, 0),
            Math.Max(mana, 0));
    }

    private static bool IsPersistedQuantityValid(byte itemSort, int quantity)
    {
        return itemSort switch
        {
            _ when ItemQuantityPolicy.IsStackableSort(itemSort) => quantity is >= ItemQuantityPolicy.MinStackQuantity
                and <= ItemQuantityPolicy.MaxStackQuantity,
            _ when ItemQuantityPolicy.IsPetSort(itemSort) => quantity is >= ItemQuantityPolicy.MinStackQuantity
                and <= ItemQuantityPolicy.MaxPetActivity,
            _ => quantity is 0 or 1
        };
    }
}

public readonly record struct PlayerEnterDataSanitization(
    IReadOnlyList<CharacterItemSlotDto> Items,
    IReadOnlyList<byte> CleanedContainers,
    IReadOnlyList<CharacterHotkeyDto> Hotkeys,
    IReadOnlyList<CharacterHotkeyDto> ClearedHotkeys,
    IReadOnlyList<CharacterCostumeSlotDto> Costumes,
    bool CostumesChanged,
    IReadOnlyList<CharacterStellarCoreSlotDto> StellarCores,
    bool StellarCoresChanged,
    int Life,
    int Mana);
