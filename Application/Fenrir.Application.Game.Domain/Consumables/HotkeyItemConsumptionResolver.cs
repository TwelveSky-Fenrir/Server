using Fenrir.Application.Game.Domain.Hotkeys;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Pure policy for CZ_USE_HOTKEY_ITEM_SEND (op22)'s item-kind hotkey-slot activation: bounds/kind/action-
///     state gating, item-category/quantity validation, the per-potion-type dispatch (life/mana/no-op gain),
///     and the hotkey-quantity decrement/clear-on-empty. No I/O, no Zone/PlayerRuntimeState/WorldDataCache
///     dependency -- same posture as <see cref="Hotkeys.HotkeyActionResolver" />/<c>BottleResolver</c>; the
///     caller (<c>UseHotkeyItemService</c>) resolves the hotkey slot, the item catalog lookup, and the
///     character's live vitals before calling in.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:2203-2492 (full <c>BEGIN_CZ(USE_HOTKEY_ITEM_SEND)</c>
///     handler) ; Server/Header/Protocol/DEFINE.h:300-301 (MAX_HOT_KEY_PAGE=3, MAX_HOT_KEY_NUM=14) ;
///     Server/Header/Protocol/DEFINE.h:370 (MAX_POTION_SORT_NUM=16) ; Server/ts25zone/S07_MyGame04.cpp:
///     2578-2612 (Stun/UnStun drive the "may eat potion" flag) ; Server/ts25zone/S07_MyGame04.cpp:1617-1654
///     (ProcessForDeath drives the same flag on death).
///     <para>
///         Action-state block list: the legacy source also names a third value (38) alongside stunned (11) and
///         dead (12), but no citation available to this resolver's originating behavior contract confirmed its
///         meaning -- deliberately NOT enforced here rather than guessed. Flagged for a follow-up
///         <c>cpp-ts25-explorer</c> re-check.
///     </para>
///     <para>
///         Potion types 6 (pet-activity), 12/13 (mutually-exclusive combat-debuff scrolls), 14/15
///         (unconditional self-buff scrolls), and 16 (mount-activity) are recognized here -- never treated as
///         malformed -- but deliberately NOT wired to their real effects yet: their wire-notification Sort
///         codes (pet/mount) and buff-display-slot indices (12-15) were not confirmed by the behavior contract
///         this resolver implements, and inventing them risks a wrong byte-exact wire shape for a future
///         consumer. Same "known but not yet implemented -&gt; clean reject, never disconnect" posture as
///         <c>Inventory.ContainerMatrix.IsImplementedContainerMoveSort</c>. Flagged for a follow-up
///         legacy-behavior-translator contract.
///     </para>
/// </remarks>
public static class HotkeyItemConsumptionResolver
{
    /// <summary>Which effect group a successful activation applied -- drives which notification(s) the caller sends.</summary>
    public enum EffectKind
    {
        /// <summary>
        ///     Types 9 (no-op) and every recognized-but-unwired type (6/12-16, all resolved as
        ///     <see cref="Outcome.RejectedClean" /> today).
        /// </summary>
        None,

        Life,
        Mana,
        LifeAndMana
    }

    public enum Outcome
    {
        /// <summary>Malformed input or corrupted/stale state -- caller must disconnect, no ack sent.</summary>
        Disconnect,

        /// <summary>Legitimate business-rule rejection -- caller sends a Result=1 ack, nothing else.</summary>
        RejectedClean,

        /// <summary>Caller sends a Result=0 ack, then whichever effect notifications actually apply.</summary>
        Success
    }

    /// <summary>
    ///     MAX_POTION_SORT_NUM (Server/Header/Protocol/DEFINE.h:370) -- documented, not itself range-checked (the switch
    ///     below already only recognizes 1-16).
    /// </summary>
    public const int MaxPotionSortNum = 16;

    /// <summary>world.Items.Sort for the generic consumable/potion family.</summary>
    public const byte ConsumableItemCategory = 2;

    /// <param name="page">0-2.</param>
    /// <param name="index">0-13.</param>
    /// <param name="slot">The hotkey table's current content at (page, index).</param>
    /// <param name="isStunned">PlayerRuntimeState.IsStunned -- action-state value 11.</param>
    /// <param name="isDead">PlayerRuntimeState.IsDead -- action-state value 12.</param>
    /// <param name="canUseConsumables">mCheckPossibleEatPotion -- gates types 1-5 only.</param>
    /// <param name="itemResolved">
    ///     Whether the caller's catalog lookup of <paramref name="slot" />'s Value1 (item id)
    ///     succeeded.
    /// </param>
    /// <param name="itemCategory">
    ///     The resolved item's world.Items.Sort -- meaningless when <paramref name="itemResolved" /> is
    ///     false.
    /// </param>
    /// <param name="potionType1">
    ///     The resolved item's iPotionType[0] (world.Items.PotionType1) -- meaningless when
    ///     <paramref name="itemResolved" /> is false.
    /// </param>
    /// <param name="potionType2">
    ///     The resolved item's iPotionType[1] (world.Items.PotionType2) -- doubles as the flat amount (types
    ///     1/3) or whole-percent value (types 2/4/5); meaningless when <paramref name="itemResolved" /> is false.
    /// </param>
    /// <param name="life">The character's current life value.</param>
    /// <param name="effectiveMaxLife">The character's current effective (equipment/buff-recomputed) max life.</param>
    /// <param name="mana">The character's current mana value.</param>
    /// <param name="effectiveMaxMana">The character's current effective (equipment/buff-recomputed) max mana.</param>
    public static Result Resolve(
        int page, int index, HotkeySlot slot,
        bool isStunned, bool isDead, bool canUseConsumables,
        bool itemResolved, byte itemCategory, int potionType1, int potionType2,
        int life, int effectiveMaxLife, int mana, int effectiveMaxMana)
    {
        if (!HotkeyActionResolver.IsValidPage(page) || !HotkeyActionResolver.IsValidIndex(index))
            return Result.DisconnectResult;

        if (slot.Kind != HotkeyBindingKind.Item)
            return Result.DisconnectResult;

        // Action-state block: stunned (11) / dead (12). See this type's own remarks for why value 38 is
        // deliberately not enforced.
        if (isStunned || isDead)
            return Result.RejectedCleanResult;

        if (!itemResolved || itemCategory != ConsumableItemCategory)
            return Result.DisconnectResult;

        if (slot.Value2 < 1)
            return Result.DisconnectResult;

        switch (potionType1)
        {
            case 1: // flat life
            case 2: // percent life
            {
                if (!canUseConsumables)
                    return Result.RejectedCleanResult;
                if (life >= effectiveMaxLife)
                    return Result.RejectedCleanResult;
                var gain = ComputeClampedGain(potionType1 == 2, potionType2, effectiveMaxLife, life);
                return Succeed(slot, EffectKind.Life, gain, 0);
            }
            case 3: // flat mana
            case 4: // percent mana
            {
                if (!canUseConsumables)
                    return Result.RejectedCleanResult;
                if (mana >= effectiveMaxMana)
                    return Result.RejectedCleanResult;
                var gain = ComputeClampedGain(potionType1 == 4, potionType2, effectiveMaxMana, mana);
                return Succeed(slot, EffectKind.Mana, 0, gain);
            }
            case 5: // combined life+mana, percentage only -- same stored percentage applied independently to each pool.
            {
                if (!canUseConsumables)
                    return Result.RejectedCleanResult;
                if (life >= effectiveMaxLife && mana >= effectiveMaxMana)
                    return Result.RejectedCleanResult;
                var lifeGain = ComputeClampedGain(true, potionType2, effectiveMaxLife, life);
                var manaGain = ComputeClampedGain(true, potionType2, effectiveMaxMana, mana);
                return Succeed(slot, EffectKind.LifeAndMana, lifeGain, manaGain);
            }
            case 9:
                // Genuine legacy no-op consumable -- always proceeds to decrement/acknowledge, zero effect.
                return Succeed(slot, EffectKind.None, 0, 0);

            case 6: // pet-activity
            case 12: // combat-debuff scroll (Assassin Scroll family)
            case 13: // combat-debuff scroll (Departed Spirit Scroll family)
            case 14: // self-buff scroll, increase hit rate
            case 15: // self-buff scroll, increase dodge rate
            case 16: // mount-activity
                // Recognized, not yet wired to a real effect -- see this type's own remarks.
                return Result.RejectedCleanResult;

            default:
                // Anything else (including outside the declared 1-16 MaxPotionSortNum range): malformed/corrupted state.
                return Result.DisconnectResult;
        }
    }

    private static Result Succeed(HotkeySlot slot, EffectKind effect, int lifeGain, int manaGain)
    {
        var remaining = slot.Value2 - 1;
        var newSlot = remaining > 0 ? slot with { Value2 = remaining } : HotkeySlot.Empty;
        return new Result(Outcome.Success, newSlot, effect, lifeGain, manaGain);
    }

    /// <summary>
    ///     Flat (raw <paramref name="potionType2" />) or whole-percent-of-<paramref name="effectiveMax" />
    ///     (truncated toward zero via integer division) gain, clamped to the pool's remaining headroom -- so a
    ///     pool with some but not all room left never overflows past its effective maximum.
    /// </summary>
    private static int ComputeClampedGain(bool isPercent, int potionType2, int effectiveMax, int current)
    {
        var raw = isPercent ? effectiveMax * potionType2 / 100 : potionType2;
        var headroom = effectiveMax - current;
        return Math.Clamp(raw, 0, headroom);
    }

    public readonly record struct Result(
        Outcome Outcome,
        HotkeySlot NewSlot,
        EffectKind Effect,
        int LifeGain,
        int ManaGain)
    {
        public static readonly Result DisconnectResult = new(Outcome.Disconnect, default, EffectKind.None, 0, 0);
        public static readonly Result RejectedCleanResult = new(Outcome.RejectedClean, default, EffectKind.None, 0, 0);
    }
}
