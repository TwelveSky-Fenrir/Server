using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Hotkeys;

/// <summary>
///     Pure policy for the auto-hunt bot's hotkey-resupply pass (<c>BotHotKey</c>/<c>BotHotKeySend</c>,
///     <c>Server/ts25zone/S07_MyGame04.cpp:2499-2559</c>): given what consumable categories are already bound to
///     the character's hotkey bar, what consumable stacks are available in inventory, which hotkey slots are
///     empty, and the four command flags / pet-and-mount presence facts, it decides which of up to four
///     category refills fire and returns the exact inventory-&gt;hotkey moves. No I/O, no
///     Zone/PlayerRuntimeState/WorldDataCache dependency -- the same "caller resolves the catalog facts, this
///     resolves the decision" posture as <see cref="HotkeyActionResolver" /> and
///     <c>Consumables.HotkeyItemConsumptionResolver</c>.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:2499-2532 (tally-by-category then the four category refills:
///     HP, MP, pet-prey, pet-food) ; :2534-2559 (<c>BotHotKeySend</c> -- move a matching inventory stack into an
///     empty hotkey slot, stamp the slot's type marker with the fixed value <c>3</c> =
///     <see cref="HotkeyBindingKind.Item" />,
///     zero the source inventory + socket slots, notify the client) ; :2526-2531 (the pet-food refill compiled
///     under <c>USE_ANIMAL</c>, unconditionally defined at Server/Header/Protocol/DEFINE.h:80 -- live in the
///     shipped build).
///     <para>
///         <b>Deliberately NOT wired into the live tick yet</b> (this policy is currently unreferenced, exactly
///         like <see cref="HotkeyActionResolver" />), for three prerequisite reasons all owned by files outside
///         this workstream -- see <see cref="Simulation.AutoHuntTickSystem" />'s own remarks for the full trail:
///         (1) <c>PlayerRuntimeState.Hotkeys</c> is never populated at world entry / zone transfer today, so
///         there is nothing live for a per-tick scan to read; (2) the resupply notification packet
///         (<c>AutoHuntHotkeyRebindResponse</c>, op120, <c>ZC_SET_HOTKEY_INVENTORY_RECV</c>) has no cited mapping
///         for its raw 3-int slot fields, so it cannot be sent byte-exactly without inventing a wire code -- a
///         fenrir-wire-protocol-implementer concern; (3) the moved hotkey binding is not persisted anywhere, so
///         applying the inventory debit without both the notification and a persistence path would desync the
///         client and risk losing the item on the next entry. Applying a move here without (1)-(3) would violate
///         the atomic-move / no-item-loss invariant, so this pass resolves the decision only and leaves its
///         application to the follow-up that closes those prerequisites.
///     </para>
///     <para>
///         <b>Category classification data gap:</b> the HP/MP families map cleanly from the established
///         <c>iPotionType[0]</c> (world.Items.PotionType1) values 1/2 (HP), 3/4 (MP), 5 (shared HP+MP) already
///         used by <c>Consumables.HotkeyItemConsumptionResolver</c>. The pet-prey and pet-food potion-type values
///         are NOT covered by any citation opened for this workstream and are deliberately not guessed here (per
///         the project's "no legacy parity from memory" rule); a caller resolves each candidate's
///         <see cref="ResupplyCategory" /> itself and simply supplies <see cref="ResupplyCategory.PetPrey" />/
///         <see cref="ResupplyCategory.PetFood" /> once those two values are cited -- until then those two refill
///         branches never fire because no candidate is ever classified into them, which is the safe direction to
///         fail toward. See <see cref="ClassifyHpMpByPotionType" /> for the cited half of the mapping.
///     </para>
/// </remarks>
public static class BotHotKeyResupplyPolicy
{
    /// <summary>
    ///     Which restore family a consumable belongs to, for the resupply tally/refill. The HP/MP values are
    ///     cited (see <see cref="ClassifyHpMpByPotionType" />); pet-prey/pet-food are caller-resolved once cited.
    /// </summary>
    public enum ResupplyCategory
    {
        /// <summary>Not a resupply-eligible consumable (or an uncited category) -- never triggers a refill.</summary>
        None,

        /// <summary>HP-restore (PotionType1 1 or 2).</summary>
        Hp,

        /// <summary>MP-restore (PotionType1 3 or 4).</summary>
        Mp,

        /// <summary>Shared HP+MP restore (PotionType1 5) -- satisfies BOTH the HP and MP "already bound" checks.</summary>
        HpMp,

        /// <summary>Pet-prey consumable (PotionType1 uncited -- caller-supplied).</summary>
        PetPrey,

        /// <summary>Pet-food / mount-food consumable (PotionType1 uncited -- caller-supplied).</summary>
        PetFood
    }

    /// <summary>
    ///     The cited half of the potion-type-&gt;category mapping (1/2 = HP, 3/4 = MP, 5 = shared HP+MP), matching
    ///     <c>Consumables.HotkeyItemConsumptionResolver</c>'s own established <c>iPotionType[0]</c> handling.
    ///     Every other value (including the uncited pet-prey/pet-food types) resolves to
    ///     <see cref="ResupplyCategory.None" /> -- see this type's own remarks.
    /// </summary>
    public static ResupplyCategory ClassifyHpMpByPotionType(int potionType1)
    {
        return potionType1 switch
        {
            1 or 2 => ResupplyCategory.Hp,
            3 or 4 => ResupplyCategory.Mp,
            5 => ResupplyCategory.HpMp,
            _ => ResupplyCategory.None
        };
    }

    /// <summary>
    ///     Resolves the (0-4) inventory-&gt;hotkey refill moves, in the legacy category order HP, MP, pet-prey,
    ///     pet-food. Each fired refill consumes one of the supplied <paramref name="emptyHotkeySlots" /> (shared
    ///     across all four moves, in order) and the first matching <paramref name="inventoryCandidates" /> entry
    ///     of the needed category; a refill whose category has no matching candidate, or which runs out of empty
    ///     hotkey slots, is silently skipped (<c>S07_MyGame04.cpp:2539-2544</c>) while the others are still
    ///     attempted.
    /// </summary>
    /// <param name="boundCategories">Categories already bound to a hotkey item slot (the tally, <c>:2506</c>).</param>
    /// <param name="inventoryCandidates">Every resupply-eligible consumable stack currently in inventory.</param>
    /// <param name="emptyHotkeySlots">Empty hotkey slots, in fill order.</param>
    /// <param name="animalPreyCmd"><c>AutoHunt.AnimalPreyCmd</c> -- the stored pet-prey command flag.</param>
    /// <param name="petEquipped">Whether a pet is equipped (Equipment pet slot occupied).</param>
    /// <param name="animalFoodCmd"><c>AutoHunt.AnimalFoodCmd</c> -- the stored pet-food command flag.</param>
    /// <param name="animalPresent">Whether a summoned animal/mount is present.</param>
    public static ImmutableArray<ResupplyMove> Resolve(
        BoundCategories boundCategories,
        IReadOnlyList<InventoryCandidate> inventoryCandidates,
        IReadOnlyList<HotkeyAddress> emptyHotkeySlots,
        bool animalPreyCmd, bool petEquipped,
        bool animalFoodCmd, bool animalPresent)
    {
        ArgumentNullException.ThrowIfNull(inventoryCandidates);
        ArgumentNullException.ThrowIfNull(emptyHotkeySlots);

        var moves = ImmutableArray.CreateBuilder<ResupplyMove>(4);
        var usedCandidates = 0; // bitmask of already-consumed inventoryCandidates indices (bounded, <= a few)
        var nextEmptySlot = 0;

        // 1. HP refill: fires when neither an HP nor a shared HP+MP consumable is already bound (:2509-2512).
        if (!boundCategories.HasHp && !boundCategories.HasHpMp)
            TryRefill(ResupplyCategory.Hp, ResupplyCategory.HpMp);

        // 2. MP refill: fires when neither an MP nor a shared HP+MP consumable is already bound (:2515-2517).
        if (!boundCategories.HasMp && !boundCategories.HasHpMp)
            TryRefill(ResupplyCategory.Mp, ResupplyCategory.HpMp);

        // 3. Pet-prey refill: none bound AND the flag is set AND a pet is equipped (:2521-2524).
        if (!boundCategories.HasPetPrey && animalPreyCmd && petEquipped)
            TryRefill(ResupplyCategory.PetPrey, ResupplyCategory.PetPrey);

        // 4. Pet-food refill (USE_ANIMAL, live): none bound AND the flag is set AND an animal is present (:2526-2531).
        if (!boundCategories.HasPetFood && animalFoodCmd && animalPresent)
            TryRefill(ResupplyCategory.PetFood, ResupplyCategory.PetFood);

        return moves.ToImmutable();

        void TryRefill(ResupplyCategory primary, ResupplyCategory alsoAccept)
        {
            if (nextEmptySlot >= emptyHotkeySlots.Count)
                return; // no empty hotkey slot left -- skip this refill (:2544)

            for (var i = 0; i < inventoryCandidates.Count; i++)
            {
                if ((usedCandidates & (1 << i)) != 0)
                    continue;

                var candidate = inventoryCandidates[i];
                if (candidate.Category != primary && candidate.Category != alsoAccept)
                    continue;

                var destination = emptyHotkeySlots[nextEmptySlot];
                moves.Add(new ResupplyMove(candidate.Page, candidate.Slot, destination.Page, destination.Index,
                    candidate.ItemId, candidate.Quantity));
                usedCandidates |= 1 << i;
                nextEmptySlot++;
                return;
            }
            // No matching stack -- skip this refill (:2539), leaving the empty slot for a later category.
        }
    }

    /// <summary>The tally of consumable categories already present on the hotkey bar (<c>S07_MyGame04.cpp:2506</c>).</summary>
    public readonly record struct BoundCategories(
        bool HasHp,
        bool HasMp,
        bool HasHpMp,
        bool HasPetPrey,
        bool HasPetFood);

    /// <summary>One resupply-eligible inventory stack the caller found (its container page/slot and item id/qty).</summary>
    public readonly record struct InventoryCandidate(
        byte Page,
        byte Slot,
        int ItemId,
        int Quantity,
        ResupplyCategory Category);

    /// <summary>A hotkey bar address (page 0-2, index 0-13).</summary>
    public readonly record struct HotkeyAddress(byte Page, byte Index);

    /// <summary>
    ///     One resolved move: the whole source inventory stack (<c>ItemId</c>/<c>Quantity</c>) is moved from
    ///     (<c>SourcePage</c>, <c>SourceSlot</c>) into the empty hotkey slot (<c>DestinationPage</c>,
    ///     <c>DestinationIndex</c>) as an item binding (type marker 3), and the source inventory slot is cleared --
    ///     a move, never a copy.
    /// </summary>
    public readonly record struct ResupplyMove(
        byte SourcePage,
        byte SourceSlot,
        byte DestinationPage,
        byte DestinationIndex,
        int ItemId,
        int Quantity);
}
