using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Domain.Hotkeys;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

/// <summary>
///     op22, CZ_USE_HOTKEY_ITEM_SEND -- resolves whatever is bound at the requested hotkey slot via
///     <see cref="HotkeyItemConsumptionResolver" />, then, on success, durably persists the decremented/
///     cleared slot to <c>game.CharacterHotkeys</c> and mirrors the slot, any life/mana gain, and any resolved
///     BUFF_INFO write (potion types 12-15) into the tick-owned <see cref="Zone" />/<see cref="PlayerRuntimeState" />
///     via <see cref="HotkeySlotMirrorZoneCommand" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:2203-2492 (full <c>BEGIN_CZ(USE_HOTKEY_ITEM_SEND)</c>
///     handler) -- see <see cref="HotkeyItemConsumptionResolver" />'s own remarks for the complete citation
///     trail (this class only adds the I/O -- catalog lookup, SQL persistence, zone-tick mirroring -- around
///     that pure resolver).
/// </remarks>
public sealed class UseHotkeyItemService(
    ICharacterRepository characters,
    WorldDataCache worldData,
    ILogger<UseHotkeyItemService> logger) : IUseHotkeyItemService
{
    public async ValueTask<UseHotkeyItemOutcome> UseAsync(Zone zone, PlayerRuntimeState state, int characterId,
        int page, int index, CancellationToken cancellationToken)
    {
        // Compare as int before narrowing to byte -- an untrusted page/index could otherwise wrap and alias a
        // real slot (same discipline this service's own predecessor used for the inventory-page check it no
        // longer performs).
        if (!HotkeyActionResolver.IsValidPage(page) || !HotkeyActionResolver.IsValidIndex(index))
            return UseHotkeyItemOutcome.Disconnect;

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
                return UseHotkeyItemOutcome.Disconnect;
            case HotkeyItemConsumptionResolver.Outcome.RejectedClean:
                return UseHotkeyItemOutcome.RejectedClean;
        }

        var newSlot = resolved.NewSlot;

        // game.CharacterHotkeys stores the raw legacy triple verbatim under (Sort, Value1, Value2), but the
        // FIRST raw int is the bound id, the SECOND the secondary value (grade/quantity), and the THIRD the
        // kind discriminator -- the exact reverse positional shift EnterWorldService's own hotkey hydration
        // uses (Zone.PlayerLifecycle.cs: "Kind <- row.Value2, HotkeySlot.Value1 <- row.Sort, HotkeySlot.Value2
        // <- row.Value1"). A Kind of None (0) here deletes the row outright rather than writing a zeroed one,
        // matching "row absence = unassigned key."
        await characters.UpsertHotkeySlotAsync(characterId, pageByte, indexByte, newSlot.Value1, newSlot.Value2,
            (byte)newSlot.Kind, cancellationToken);

        // LifeGain/ManaGain are deltas, recomputed against the LIVE state.Life/state.Mana at apply time by
        // Zone.ApplyHotkeySlotMirrorCommand -- NOT precomputed here against this session-thread snapshot,
        // which could otherwise silently clobber a concurrent tick-thread mutation (combat damage, regen,
        // another potion) between this read and the command's drain. See HotkeySlotMirrorZoneCommand's own
        // remarks.
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

        return UseHotkeyItemOutcome.Success;
    }
}
