using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class UseHotkeyItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<UseHotkeyItemService> logger) : IUseHotkeyItemService
{
    public async ValueTask<UseHotkeyItemOutcome> UseAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int page, int index, CancellationToken cancellationToken)
    {
        if (!HotkeyActionResolver.IsValidPage(page) || !HotkeyActionResolver.IsValidIndex(index))
        {
            logger.LogInformation(
                "Character {CharacterId} use-hotkey-item disconnect: invalid page/index ({Page}:{Index})",
                characterId, page, index);
            return UseHotkeyItemOutcome.Disconnect;
        }

        var pageByte = (byte)page;
        var indexByte = (byte)index;
        var slot = state.GetHotkeySlot(pageByte, indexByte);

        var itemResolved = false;
        byte itemCategory = 0;
        var potionType1 = 0;
        var potionType2 = 0;
        if (slot.Kind == HotkeyBindingKind.Item &&
            worldData.ItemsById.TryGetValue(slot.Value1, out var itemDefinition))
        {
            itemResolved = true;
            itemCategory = itemDefinition.Item.Sort;
            potionType1 = itemDefinition.Item.PotionType1;
            potionType2 = itemDefinition.Item.PotionType2;
        }

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var maxMana = state.Stats?.MaxMana ?? state.MaxMana;

        var resolved = HotkeyItemConsumptionResolver.Resolve(page, index, slot, state.IsStunned, state.IsDead,
            state.CanUseConsumables, itemResolved, itemCategory, potionType1, potionType2,
            state.Life, maxLife, state.Mana, maxMana);

        switch (resolved.Outcome)
        {
            case HotkeyItemConsumptionResolver.Outcome.Disconnect:
                logger.LogInformation(
                    "Character {CharacterId} use-hotkey-item disconnect: resolver rejected slot ({Page}:{Index}) as malformed/stale",
                    characterId, page, index);
                return UseHotkeyItemOutcome.Disconnect;
            case HotkeyItemConsumptionResolver.Outcome.RejectedClean:
                logger.LogDebug(
                    "Character {CharacterId} use-hotkey-item rejected (clean) at slot ({Page}:{Index})",
                    characterId, page, index);
                return UseHotkeyItemOutcome.RejectedClean;
        }

        var newSlot = resolved.NewSlot;

        await characters.UpsertHotkeySlotAsync(characterId, pageByte, indexByte, newSlot.Value1, newSlot.Value2,
            (byte)newSlot.Kind, cancellationToken);

        int? lifeGain = resolved.Effect is HotkeyItemConsumptionResolver.EffectKind.Life or
            HotkeyItemConsumptionResolver.EffectKind.LifeAndMana
            ? resolved.LifeGain
            : null;
        int? manaGain = resolved.Effect is HotkeyItemConsumptionResolver.EffectKind.Mana or
            HotkeyItemConsumptionResolver.EffectKind.LifeAndMana
            ? resolved.ManaGain
            : null;

        if (!zone.PostHotkeySlotMirrorCommand(new HotkeySlotMirrorZoneCommand(characterId, pageByte, indexByte,
                newSlot, lifeGain, manaGain, resolved.BuffWrites)))
            logger.LogError(
                "Zone {MapId} hotkey-slot inbox full: dropped hotkey-item-use mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} use-hotkey-item applied: slot ({Page}:{Index}) effect {Effect}, life+{LifeGain} mana+{ManaGain}",
            characterId, page, index, resolved.Effect, lifeGain ?? 0, manaGain ?? 0);

        return UseHotkeyItemOutcome.Success;
    }
}
