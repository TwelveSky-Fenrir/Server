using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Skills;

/// <summary>
///     Server-authoritative skill-cast grade helpers -- <c>MyUtil::GetMaxSkillGradeNum</c> ("Skill Hack1"
///     bound check) and <c>MyFactor::GetBonusSkillValue</c>, per the D2-skillcast-helpers behavior contract.
///     Both are pure, read-only, allocation-free computations: no I/O, no mutation, no disconnect/log
///     decision of their own -- callers (<see cref="AntiCheat.SkillCastGuard" /> and friends) own what to do
///     with the result.
/// </summary>
/// <remarks>
///     <para>
///         <b>GetMaxSkillGradeNum was already duplicated once</b> as a private per-class copy inside
///         <see cref="AutoBuffSkillResolver" /> (its own <c>GetMaxSkillGradeNum</c> private method) -- this
///         class is the shared, public replacement the D2 contract asks for, so a future integration pass can
///         delete that private copy and call <see cref="GetMaxSkillGradeNum" /> instead (see wiringManifest;
///         not done here, since editing an existing file is out of scope for this slice). The two
///         implementations are behaviourally identical: both scan the learned-skill slot table for a matching
///         skill id and return its stored grade, or -1 if the skill is not known.
///     </para>
///     <para>
///         <b>
///             GetBonusSkillValue composes already-verified Fenrir building blocks rather than re-deriving the
///             legacy formula from scratch.
///         </b>
///         Two of the six contribution terms the D2 contract describes
///         (pet-amulet skill-specific bonus, pet-growth-tier bonus-skill grade) turn out to already be
///         implemented, fully cited, and already tested, in <see cref="StatCalculator" />'s pet-contribution
///         partial (<c>StatCalculator.PetContribution.cs</c>, workstream B8) --
///         <see cref="StatCalculator.PetBonusSkillStatType" />
///         and <see cref="StatCalculator.PetGradedIuBonus" /> cite the exact same
///         <c>GameSystem_07_Pet.cpp:1327-1351</c> range the D2 contract cites for its "six hardcoded skill
///         identifiers, six pet-amulet sub-categories" term, and
///         <see cref="StatCalculator.PetGrowthValueBonusSkillGrade" />
///         cites <c>MyFactor.cpp:4572</c>, inside the D2 contract's own <c>MyFactor.cpp:4519-4578</c> citation
///         range for <c>GetBonusSkillValue</c> as a whole. Reusing them here is the "grep for an existing
///         partial implementation and extend, don't duplicate" instruction applied to a cross-project match,
///         not an invention -- see each term's own remarks below for exactly which contribution this composes.
///     </para>
///     <para>
///         <b>Three of the six terms are NOT modeled here and always contribute zero</b> -- see the per-term
///         remarks on <see cref="GetBonusSkillValue" /> and this project's own instruction to never invent an
///         id/magnitude the source contract does not transcribe:
///     </para>
///     <list type="number">
///         <item>
///             <description>
///                 <b>Term 3 (cape-slot flat +2)</b>: the D2 contract states "one specific hardcoded item
///                 identifier" grants a flat +2 to every skill query when equipped in the cape slot, but does not
///                 transcribe that item id anywhere in its Outputs or Citations sections. Left as a documented
///                 no-op. NOTE for whoever resolves this:
///                 <c>Fenrir.Application.Game.Domain.Enchant.CapeUpgradeResolver.EmperorCapeItemId</c>
///                 (94100) is an UNCONFIRMED lead only -- it comes from a completely different mechanic (the cape
///                 upgrade RNG target table) cited to a different <c>Server/</c> range than this term's
///                 <c>MyFactor.cpp:4519-4578</c>, and must not be assumed to be the same item without an
///                 independent citation.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Term 6 (guild-buff type-3 flat +1)</b>: the numeric gate itself is fully specified (guild
///                 buff active, numeric type selector == 3, flat +1 -- see <see cref="GuildBuffFlatBonusType" />/
///                 <see cref="GuildBuffFlatBonusAmount" />), but the D2 contract's own "buff-category skill in the
///                 shared skill-definition table" predicate has no field name or threshold transcribed (only its
///                 citation, <c>GameSystem_03_Skill.cpp:22-33</c>, a "NULL-safe skill-definition lookup"). Left as
///                 a documented no-op via <see cref="IsBuffCategorySkill" /> pending that predicate.
///             </description>
///         </item>
///         <item>
///             <description>
///                 <b>Terms 4/5's <c>petPackedValue</c> input has no confirmed live source yet in Fenrir</b> --
///                 this is a PRE-EXISTING gap flagged by <c>StatCalculator.PetContribution.cs</c>'s own remarks
///                 (workstream B8), not something introduced here: the packed 4-signed-byte value those two terms
///                 decode is only modeled in Fenrir today as <see cref="World.PlayerRuntimeState.PetGrowth" />, a
///                 plain growth COUNTER for a sort-22 growth pet, not the packed graded encoding a sort-28/31/32/33
///                 amulet/utility pet needs. Passing <c>petPackedValue: 0</c> (the only value any current caller
///                 can honestly supply) is safe and NOT a false-positive risk: byte-decoding 0 never satisfies
///                 either term's tens-digit-equals-type gate (<see cref="StatCalculator.PetGradedIuBonus" />) or
///                 code-membership switch (<see cref="StatCalculator.PetGrowthValueBonusSkillGrade" />), so both
///                 terms stay inert at exactly 0 until the real input lands -- unlike terms 3 and 6 above, this is
///                 not a source of client/server disagreement for any real player today.
///             </description>
///         </item>
///     </list>
/// </remarks>
public static class SkillGradeAuthority
{
    /// <summary>Server/Header/Protocol/DEFINE.h:281 -- the 13 equip slots GetBonusSkillValue scans.</summary>
    public const int EquipSlotCount = 13;

    /// <summary>Server/Header/Protocol/STRUCT.h:1662-1676 -- the cape equip-slot index (term 3's slot).</summary>
    public const int CapeSlotIndex = 1;

    /// <summary>
    ///     Server/Header/Protocol/STRUCT.h:1662-1676 -- the pet/amulet equip-slot index (terms 4/5's slot).
    ///     Matches the already-established <see cref="PetSlots.EquipmentSlot" /> constant (same slot, already
    ///     cited by a different contract) rather than re-deriving a second literal 8.
    /// </summary>
    public const int PetSlotIndex = PetSlots.EquipmentSlot;

    /// <summary>
    ///     FITEM_SORT::INEW_PET -- matches <c>StatCalculator.PetContribution.cs</c>'s own private
    ///     <c>PetAmuletSort</c> constant (same sourced value, not re-invented). Term 4's amulet gate and term
    ///     5's "different, non-overlapping item category" gate both key off this value.
    /// </summary>
    private const int PetAmuletSort = 28;

    /// <summary>
    ///     One of exactly four numeric guild-buff "type" selectors (D2 contract Edge cases: 1/2/4 apply
    ///     elsewhere in <c>MyFactor.cpp</c>, only 3 applies to <see cref="GetBonusSkillValue" />'s term 6). No
    ///     human-readable label exists in any citation opened for the D2 contract -- open question, do not
    ///     invent one.
    /// </summary>
    private const int GuildBuffFlatBonusType = 3;

    /// <summary>Term 6's flat contribution once the (unresolved) buff-category predicate is satisfied.</summary>
    private const int GuildBuffFlatBonusAmount = 1;

    /// <summary>
    ///     <c>MyUtil::GetMaxSkillGradeNum</c> (Server/ts25zone/S07_MyGame03.cpp:8945-8956) -- NOT a
    ///     level/class/table-derived "maximum grade the design allows" despite the name; it is a direct echo
    ///     of whatever was last written into the caller's own learned-skill slot for <paramref name="skillId" />.
    ///     Returns -1 (never a legitimate grade) when the skill is not currently learned -- callers comparing
    ///     a claimed grade against this result must treat -1 as "not learned," not special-case 0.
    /// </summary>
    /// <param name="skillId">The skill being queried.</param>
    /// <param name="learnedSkills">The querying character's OWN learned-skill slots (never an arbitrary target's).</param>
    public static int GetMaxSkillGradeNum(int skillId, IReadOnlyDictionary<byte, LearnedSkill> learnedSkills)
    {
        foreach (var learned in learnedSkills.Values)
            if (learned.SkillId == skillId)
                return learned.Grade;

        return -1;
    }

    /// <summary>
    ///     <c>MyFactor::GetBonusSkillValue</c> (Server/Header/Protocol/MyFactor.cpp:4519-4578) -- the sum of
    ///     six independently-gated contributions for <paramref name="skillId" />, evaluated against the
    ///     querying character's OWN equipment and guild-buff state (never an arbitrary target's, and never
    ///     read from the client). Terms 3 and 6 are documented no-ops pending missing citations (see class
    ///     remarks); terms 4/5 are wired but currently inert pending a confirmed <paramref name="petPackedValue" />
    ///     source (also class remarks). Terms 1 and 2 are fully modeled.
    /// </summary>
    /// <param name="skillId">The skill being queried.</param>
    /// <param name="equipSlotItems">
    ///     The querying character's <see cref="EquipSlotCount" /> equip slots, one <see cref="ItemDefinition" />
    ///     per occupied slot (null = empty), indexed exactly as legacy's equip-slot enum (cape =
    ///     <see cref="CapeSlotIndex" />, pet = <see cref="PetSlotIndex" />). A span shorter than
    ///     <see cref="EquipSlotCount" /> is tolerated (defensive; terms 3/4/5 simply cannot fire past the end)
    ///     rather than throwing, since this is a hot validation path a malformed caller must never crash.
    /// </param>
    /// <param name="petPackedValue">
    ///     The pet slot's packed 4-signed-byte sub-value (Server/Header/function.h:317-343) terms 4/5 decode.
    ///     Pass 0 until Fenrir has a confirmed source for this field (see class remarks) -- 0 always yields a
    ///     0 contribution from both terms, never a false mismatch.
    /// </param>
    /// <param name="skillDefinition">
    ///     The queried skill's own catalog row, needed only for term 6's (currently unresolved) buff-category
    ///     predicate. Accepted now so this method's signature does not need to change once that predicate is
    ///     resolved.
    /// </param>
    /// <param name="guildBuffType">The character's live guild-buff numeric "type" selector (one of 1/2/3/4).</param>
    /// <param name="guildBuffActive">The character's live guild-buff on/off gate.</param>
    public static int GetBonusSkillValue(
        int skillId,
        ReadOnlySpan<ItemDefinition?> equipSlotItems,
        int petPackedValue,
        SkillDefinition? skillDefinition,
        int guildBuffType,
        bool guildBuffActive)
    {
        var total = 0;

        for (var slotIndex = 0; slotIndex < equipSlotItems.Length; slotIndex++)
        {
            var equipped = equipSlotItems[slotIndex];
            if (equipped is null)
                continue;

            // Term 1: skill-specific per-item bonus-skill table (up to 8 entries, STRUCT.h:42-43,91) --
            // EVERY matching entry is summed, not just the first (an item template can list the same skill
            // more than once).
            foreach (var bonus in equipped.BonusSkills)
                if (bonus.SkillId == skillId)
                    total += bonus.Value;

            // Term 2: unconditional per-slot flat add -- the third of three generic "auxiliary" fields
            // present on EVERY item template (ItemRowDto.CapeInfo3, STRUCT.h:41,90), added once per
            // non-empty slot for every skill query alike, regardless of slot or skill. NOT restricted to the
            // cape slot despite the field's name -- do not narrow this without a separately-approved product
            // decision (D2 contract Edge cases).
            total += equipped.Item.CapeInfo3;

            // Term 3 (cape-slot-specific flat +2 for one hardcoded item id): deliberately NOT modeled -- see
            // class remarks. `if (slotIndex == CapeSlotIndex && equipped.Item.ItemId == /* unresolved */) total += 2;`
        }

        // Terms 4 + 5: pet slot only.
        if (PetSlotIndex < equipSlotItems.Length && equipSlotItems[PetSlotIndex] is { } petItem)
        {
            // Term 4: six hardcoded skill ids -> six pet-amulet sub-categories (StatCalculator.PetBonusSkillStatType),
            // packed-byte-encoded magnitude (StatCalculator.PetGradedIuBonus, itself gated to the amulet sort
            // plus its own internal six-item-id exclusion list -- see that method's own remarks). A skill id
            // outside the six mapped values yields statType 0, which PetGradedIuBonus's tens-digit gate can
            // never match, so this contributes 0 for every other skill query, matching the contract's "for
            // exactly six hardcoded skill identifiers" scope.
            var amuletStatType = StatCalculator.PetBonusSkillStatType(skillId);
            if (amuletStatType != 0)
                total += StatCalculator.PetGradedIuBonus(petItem.Item.ItemId, petItem.Item.Sort, petPackedValue,
                    amuletStatType);

            // Term 5: pet-growth-tier bonus-skill grade, gated to "not the amulet sort" -- the D2 contract's
            // own "different, non-overlapping pet-slot item category than term 4's" edge case. APPROXIMATE:
            // the precise growth-pet item-sort whitelist StatCalculator.PetContribution.cs's own remarks
            // mention (sorts 31/32/33, "OUT OF SLICE" there too) is not confirmed for this exact call site --
            // see openQuestions. Using "sort != amulet" as the gate is the closest citation-backed
            // approximation available without inventing the whitelist.
            if (petItem.Item.Sort != PetAmuletSort)
                total += StatCalculator.PetGrowthValueBonusSkillGrade(petPackedValue);
        }

        // Term 6 (guild-buff type-3 flat +1 for a buff-category skill): the type/active gate is fully
        // modeled; IsBuffCategorySkill always returns false today -- see class remarks and that method's own
        // TODO.
        if (guildBuffActive && guildBuffType == GuildBuffFlatBonusType && IsBuffCategorySkill(skillDefinition))
            total += GuildBuffFlatBonusAmount;

        return total;
    }

    /// <summary>
    ///     TODO(D2-skillcast-helpers open question): the buff-category predicate itself (which
    ///     <see cref="SkillRowDto" /> field, and what value/range counts as "buff category") is not
    ///     transcribed anywhere in the D2 contract's Outputs/Edge-cases text -- only its citation
    ///     (Server/ts25zone/GameSystem/GameSystem_03_Skill.cpp:22-33, a NULL-safe skill-definition lookup) is
    ///     given, with no field name or threshold. Per this project's never-invent-a-magnitude rule, this
    ///     always returns false (so term 6 never contributes) until a follow-up contract or a
    ///     <c>legacy-behavior-translator</c>/<c>fenrir-gameplay-domain-engineer</c> pass supplies the actual
    ///     predicate. <paramref name="skillDefinition" /> is accepted (and referenced, so the parameter is not
    ///     flagged unused) so <see cref="GetBonusSkillValue" />'s public signature does not need to change
    ///     once this is resolved.
    /// </summary>
    private static bool IsBuffCategorySkill(SkillDefinition? skillDefinition)
    {
        _ = skillDefinition;
        return false;
    }
}
