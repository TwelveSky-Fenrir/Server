using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Pure resolver for the general item-based "skill grimoire" consumption path -- world.Items iSort==5's
///     fallthrough case of CZ_USE_INVENTORY_ITEM_SEND (e.g. items 99011-99013 "Book of Nobel Dragon/Royal
///     Serpent/Grand Tiger" and the per-tribe 90101-90118 endgame grimoire family). Learning an item's
///     granted skill is gated on the item's own tribe restriction, the avatar's combined level, a duplicate-
///     skill check, the skill catalog resolving at all, a skill-point balance check, and finally a free-slot
///     search within the skill's category sub-range. Excludes the three tribe-conversion book ids
///     (99014/99015/99016) entirely -- those are a distinct, separately-gated special case (their own
///     tribe/level/social-link checks and equip+skill tribe-conversion side effects) that this resolver does
///     not cover; the caller is responsible for routing those three ids elsewhere. No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2119-2123 (<c>switch(ttitem-&gt;iSort)</c>, case 5, gated by
///     Server/Header/use_inventory.h:3's unconditionally-defined skill-scroll feature flag) ;
///     Server/ts25zone/S04_MyWork03.cpp:2124-2227 (the 99014/15/16 tribe-conversion-book sub-case, out of
///     scope here, confirmed live in every build variant by Server/Header/Protocol/DEFINE.h:21-31) ;
///     Server/ts25zone/S04_MyWork03.cpp:2229-2236 (the tribe gate -- checked against the equipping
///     character's aPreviousTribe, mirroring <see cref="EquipItemValidationGate" />'s own equip-time tribe
///     check, not the character's current tribe) ; :2237-2241 (combined-level check, aLevel1+aLevel2) ;
///     :2242-2253 (duplicate-skill search across all 40 slots) ; :2254-2264 (skill-catalog miss and
///     insufficient-skill-point rejections) ; :2265-2346 (the four skill-category slot-range searches --
///     shared verbatim with <see cref="SkillLearnResolver" />'s own <c>TryFindFreeSlotForType</c>, since both
///     resolvers read the identical legacy table -- and the unrecognized-category default rejection) ;
///     Server/Header/Protocol/DEFINE.h:298 (the 40-slot bound) ; Server/Header/Protocol/STRUCT.h:133-143
///     (skill reference-table fields read here -- category code, point cost) ;
///     Database/Migrations/Seed/world/080_items.sql:72538-72546 (items 99011-99013, confirms Sort==5 and the
///     "any tribe" sentinel) ; Database/Migrations/Seed/world/080_items.sql:68278-68331 (items 90101-90118,
///     confirms Sort==5 with a genuine per-tribe restriction code, live/exercised tribe-gate content) ;
///     Database/Migrations/Seed/world/080_items.sql:1-6 (generator header: a raw item's granted-skill value
///     of zero is translated to NULL at import -- see this type's own remarks on why that sidesteps the
///     legacy's own duplicate-skill-check ambiguity for a zero granted-skill id).
/// </remarks>
public static class SkillGrimoireLearnResolver
{
    public enum Outcome
    {
        WrongTribe,
        LevelTooLow,
        AlreadyLearned,
        UnknownSkill,
        InsufficientSkillPoints,

        /// <summary>
        ///     Collapses two distinct legacy rejections that share an identical outward effect (a disconnect,
        ///     per this behavior's own contract): no empty slot found in the skill's category sub-range(s),
        ///     and the category code itself not matching any of the four handled values
        ///     (S04_MyWork03.cpp:2265-2346's own default case). Neither is independently observable by the
        ///     caller, so a single outcome value is enough.
        /// </summary>
        NoFreeSlot,

        Success
    }

    /// <param name="itemTribeRestriction">
    ///     The resolved item's iEquipInfo[0] -- see
    ///     <see cref="EquipItemValidationGate.AnyTribeSentinel" />/
    ///     <see cref="EquipItemValidationGate.TribeRestrictionOffset" /> for the sentinel/offset convention this shares.
    /// </param>
    /// <param name="itemLevelLimit">The resolved item's iLevelLimit.</param>
    /// <param name="itemMartialLevelLimit">
    ///     The resolved item's iMartialLevelLimit -- summed with
    ///     <paramref name="itemLevelLimit" /> for the combined-level check.
    /// </param>
    /// <param name="grantedSkillId">
    ///     The resolved item's GainSkillNumber -- null means "no skill configured" (the legacy raw-0
    ///     sentinel, already translated at import; see this type's own remarks).
    /// </param>
    /// <param name="characterPreviousTribe">The consuming character's aPreviousTribe (not its current tribe).</param>
    /// <param name="characterCombinedLevel">Sum of the character's two level-tracking fields (aLevel1+aLevel2).</param>
    /// <param name="skillDefinition">The catalog row for <paramref name="grantedSkillId" />, or null if it did not resolve.</param>
    /// <param name="learnedSkills">The character's current 40 skill slots.</param>
    /// <param name="currentSkillPoints">The character's current unspent skill-point balance.</param>
    public static Result Resolve(
        int itemTribeRestriction,
        int itemLevelLimit,
        int itemMartialLevelLimit,
        int? grantedSkillId,
        byte characterPreviousTribe,
        int characterCombinedLevel,
        SkillDefinition? skillDefinition,
        IReadOnlyDictionary<byte, LearnedSkill> learnedSkills,
        int currentSkillPoints)
    {
        if (itemTribeRestriction != EquipItemValidationGate.AnyTribeSentinel &&
            itemTribeRestriction - EquipItemValidationGate.TribeRestrictionOffset != characterPreviousTribe)
            return Result.Fail(Outcome.WrongTribe);

        if (itemLevelLimit + itemMartialLevelLimit > characterCombinedLevel)
            return Result.Fail(Outcome.LevelTooLow);

        // Duplicate-skill search runs on the item's raw granted-skill id BEFORE the catalog lookup below,
        // matching the legacy call order (S04_MyWork03.cpp:2242-2253 precedes :2254-2264). A null
        // grantedSkillId (the "no skill configured" sentinel) is skipped entirely rather than compared as a
        // literal 0: LearnedSkills models an empty slot as an absent dictionary key, never a stored zero, so
        // -- unlike the legacy aSkill[][0] array, whose own empty-slot convention makes this same check
        // ambiguous for a genuinely-zero granted-skill id -- no real slot could ever match a zero here anyway.
        if (grantedSkillId is { } requestedSkillId)
            foreach (var learned in learnedSkills.Values)
                if (learned.SkillId == requestedSkillId)
                    return Result.Fail(Outcome.AlreadyLearned);

        if (grantedSkillId is null || skillDefinition is not { } skill || skill.Skill.SkillId != grantedSkillId)
            return Result.Fail(Outcome.UnknownSkill);

        if (currentSkillPoints < skill.Skill.LearnSkillPoint)
            return Result.Fail(Outcome.InsufficientSkillPoints);

        return SkillLearnResolver.TryFindFreeSlotForType(skill.Skill.Type, learnedSkills, out var slot)
            ? new Result(Outcome.Success, slot, skill.Skill.SkillId, skill.Skill.LearnSkillPoint)
            : Result.Fail(Outcome.NoFreeSlot);
    }

    public readonly record struct Result(Outcome Outcome, byte Slot, int SkillId, int Cost)
    {
        public static Result Fail(Outcome outcome)
        {
            return new Result(outcome, 0, 0, 0);
        }
    }
}
