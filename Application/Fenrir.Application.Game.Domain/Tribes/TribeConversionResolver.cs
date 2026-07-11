using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Tribes;

/// <summary>
///     Server-authoritative tribe-conversion equivalence resolver -- the missing consumer of the imported
///     world.usp_TribeConversionCatalog_GetAll catalog DTOs (<see cref="TribeSkillEquivalenceRowDto" /> /
///     <see cref="TribeItemEquivalenceRowDto" /> / <see cref="TribeCostumeEquivalenceRowDto" />). Legacy
///     TChangeSkill (Server/ts25zone/S04_MyWork03.cpp:167-238) and TChangeEquip (:509-585) remap through the
///     tsSkill/tsItem/tsFation arrays these three tables mirror; before this class they had a schema, seed data,
///     a repository read, and DTOs, but no C# code that actually resolved a source-tribe id to its target-tribe
///     equivalent.
///     <para>
///         Built ONCE at GameServer boot from the read-only catalog (every world.* table is seed-only reference
///         data), then queried allocation-free on the use-item hot path -- the lookups are
///         <see cref="FrozenDictionary{TKey,TValue}" />s
///         keyed on value-tuple structs, so a resolve is a couple of struct-key probes and no allocation. Pure:
///         no I/O, no per-tick recompute.
///     </para>
///     <para>
///         The resolve primitives are best-effort by return value: a <c>false</c> means "no target-tribe
///         equivalent". The two legacy item paths differ only in how they react to that -- the scroll path
///         (game.usp_Character_ApplyTribeScrollConversion, TChangeTribe) leaves an un-mappable slot as-is, while
///         the book path (game.usp_Character_ApplyTribeConversion, TChangeEquip) aborts the whole conversion
///         (see <see cref="AreAllItemsMappable" />). Both share the same equivalence data resolved here.
///     </para>
/// </summary>
public sealed class TribeConversionResolver
{
    // The 3 playable base tribes (0=Noble Dragon, 1=Royal Serpent, 2=Grand Tiger); 3 is the neutral "nangin"
    // pool. Every equivalence table is exactly 3 columns wide, so only 0/1/2 ever participate in a remap
    // (Server/ts25zone/S07_MyGame03.cpp:11413-11523).
    public const byte NobleDragon = 0;
    public const byte RoyalSerpent = 1;
    public const byte GrandTiger = 2;
    public const byte Neutral = 3;

    // Tribe-change books (S04_MyWork03.cpp:2119-2131): toTribe = itemId - 99014, so the SERVER derives the
    // target from the consumed item alone. The client never supplies it, which is why the book path has no
    // spoofable target field to guard (unlike the scroll path below).
    public const int BookNobleDragon = 99014;
    public const int BookRoyalSerpent = 99015;
    public const int BookGrandTiger = 99016;

    // Faction-transfer scrolls (S04_MyWork03.cpp:4740-4742). Unlike the books, the target tribe is client-
    // supplied (the use-item request's value field), so it MUST be range-checked with IsPlayableTribe before
    // use -- the legacy guard "target < 0 AND target > 2" (:4745) is inert and never rejects anything, a
    // hardening gap this resolver closes.
    public const int ScrollA = 8153;
    public const int ScrollB = 8154;

    // Reserved "V2" alternate equipment id band: 87129..87257 are the base 87000..87128 rows offset by +129 and
    // are NOT separate catalog rows -- pure arithmetic, exactly as the legacy bSetV2 branch and
    // game.usp_Character_ApplyTribeConversion both apply it (see Tables/world/TribeItemEquivalences.sql).
    private const int V2BandLow = 87129;
    private const int V2BandHigh = 87257;
    private const int V2Offset = 129;

    // (sourceTribe, sourceItemId) -> GroupIndex, then (GroupIndex, targetTribe) -> targetItemId. Split into two
    // dictionaries rather than one nested lookup so a single row seeds both directions with no per-query fan-out.
    private readonly FrozenDictionary<(byte Tribe, int ItemId), byte> _costumeGroupBySourceItem;
    private readonly FrozenDictionary<(byte Group, byte Tribe), int> _costumeIdByGroupTribe;
    private readonly FrozenDictionary<(byte Tribe, int ItemId), byte> _itemGroupBySourceItem;
    private readonly FrozenDictionary<(byte Group, byte Tribe), int> _itemIdByGroupTribe;
    private readonly FrozenDictionary<(byte Tribe, int SkillId), byte> _skillGroupBySourceSkill;
    private readonly FrozenDictionary<(byte Group, byte Tribe), int> _skillIdByGroupTribe;

    public TribeConversionResolver(
        IReadOnlyList<TribeSkillEquivalenceRowDto> skillEquivalences,
        IReadOnlyList<TribeItemEquivalenceRowDto> itemEquivalences,
        IReadOnlyList<TribeCostumeEquivalenceRowDto> costumeEquivalences)
    {
        ArgumentNullException.ThrowIfNull(skillEquivalences);
        ArgumentNullException.ThrowIfNull(itemEquivalences);
        ArgumentNullException.ThrowIfNull(costumeEquivalences);

        var skillGroup = new Dictionary<(byte, int), byte>(skillEquivalences.Count);
        var skillById = new Dictionary<(byte, byte), int>(skillEquivalences.Count);
        foreach (var row in skillEquivalences)
        {
            skillGroup[(row.TribeId, row.SkillId)] = row.GroupIndex;
            skillById[(row.GroupIndex, row.TribeId)] = row.SkillId;
        }

        var itemGroup = new Dictionary<(byte, int), byte>(itemEquivalences.Count);
        var itemById = new Dictionary<(byte, byte), int>(itemEquivalences.Count);
        foreach (var row in itemEquivalences)
        {
            itemGroup[(row.TribeId, row.ItemId)] = row.GroupIndex;
            itemById[(row.GroupIndex, row.TribeId)] = row.ItemId;
        }

        var costumeGroup = new Dictionary<(byte, int), byte>(costumeEquivalences.Count);
        var costumeById = new Dictionary<(byte, byte), int>(costumeEquivalences.Count);
        foreach (var row in costumeEquivalences)
        {
            costumeGroup[(row.TribeId, row.ItemId)] = row.GroupIndex;
            costumeById[(row.GroupIndex, row.TribeId)] = row.ItemId;
        }

        _skillGroupBySourceSkill = skillGroup.ToFrozenDictionary();
        _skillIdByGroupTribe = skillById.ToFrozenDictionary();
        _itemGroupBySourceItem = itemGroup.ToFrozenDictionary();
        _itemIdByGroupTribe = itemById.ToFrozenDictionary();
        _costumeGroupBySourceItem = costumeGroup.ToFrozenDictionary();
        _costumeIdByGroupTribe = costumeById.ToFrozenDictionary();
    }

    /// <summary>
    ///     The three playable base tribes are 0/1/2; 3 is the neutral pool and the equivalence tables have no
    ///     column for it. This is the real range guard the scroll path's inert legacy check
    ///     (S04_MyWork03.cpp:4745) should have been -- always apply it to a client-supplied target tribe before
    ///     passing it to the stored procedure, never trust the wire value.
    /// </summary>
    public static bool IsPlayableTribe(byte tribe)
    {
        return tribe <= GrandTiger;
    }

    /// <summary>
    ///     Derives the target tribe for a tribe-change book from the consumed item id alone (99014 -> 0,
    ///     99015 -> 1, 99016 -> 2), matching the legacy <c>toTribe = itemId - 99014</c> derivation. Returns
    ///     <c>false</c> for any non-book id so a caller can never be tricked into a conversion by a spoofed item.
    /// </summary>
    public bool TryGetBookTargetTribe(int itemId, out byte toTribe)
    {
        switch (itemId)
        {
            case BookNobleDragon:
                toTribe = NobleDragon;
                return true;
            case BookRoyalSerpent:
                toTribe = RoyalSerpent;
                return true;
            case BookGrandTiger:
                toTribe = GrandTiger;
                return true;
            default:
                toTribe = 0;
                return false;
        }
    }

    /// <summary>
    ///     Resolves one equipment item's target-tribe equivalent, applying the +129 "V2" band arithmetic
    ///     transparently. On <c>false</c>, <paramref name="newItemId" /> is left equal to
    ///     <paramref name="itemId" /> so a best-effort caller can write it back unchanged; the book path treats
    ///     the same <c>false</c> as an abort trigger instead.
    /// </summary>
    public bool TryRemapItem(byte fromTribe, byte toTribe, int itemId, out int newItemId)
    {
        newItemId = itemId;

        var isV2 = itemId is >= V2BandLow and <= V2BandHigh;
        var baseId = isV2 ? itemId - V2Offset : itemId;

        if (!_itemGroupBySourceItem.TryGetValue((fromTribe, baseId), out var group))
            return false;
        if (!_itemIdByGroupTribe.TryGetValue((group, toTribe), out var targetBase))
            return false;

        newItemId = isV2 ? targetBase + V2Offset : targetBase;
        return true;
    }

    /// <summary>Resolves one learned/hotkey-bound skill's target-tribe equivalent (tsSkill remap).</summary>
    public bool TryRemapSkill(byte fromTribe, byte toTribe, int skillId, out int newSkillId)
    {
        newSkillId = skillId;

        if (!_skillGroupBySourceSkill.TryGetValue((fromTribe, skillId), out var group))
            return false;
        if (!_skillIdByGroupTribe.TryGetValue((group, toTribe), out var targetSkill))
            return false;

        newSkillId = targetSkill;
        return true;
    }

    /// <summary>
    ///     Resolves one worn-costume item's target-tribe equivalent (tsFation remap). Fenrir has no durable
    ///     per-character costume-wardrobe storage yet, so this is the lookup an Application-layer follow-up uses
    ///     to remap the in-memory wardrobe -- neither stored procedure persists costumes today (see
    ///     Tables/world/TribeCostumeEquivalences.sql).
    /// </summary>
    public bool TryRemapCostume(byte fromTribe, byte toTribe, int itemId, out int newItemId)
    {
        newItemId = itemId;

        if (!_costumeGroupBySourceItem.TryGetValue((fromTribe, itemId), out var group))
            return false;
        if (!_costumeIdByGroupTribe.TryGetValue((group, toTribe), out var targetItem))
            return false;

        newItemId = targetItem;
        return true;
    }

    /// <summary>
    ///     Book-path (TChangeEquip) all-or-nothing precondition: the conversion aborts if any occupied,
    ///     non-pet equipment slot has no target-tribe equivalent (Server/ts25zone/S04_MyWork03.cpp:555-562).
    ///     Lets the caller decide to reject the whole book use before touching any durable state, mirroring what
    ///     game.usp_Character_ApplyTribeConversion re-checks server-side (THROW 50320). The scroll path does NOT
    ///     use this -- it is best-effort per slot and leaves un-mappable gear alone.
    /// </summary>
    public bool AreAllItemsMappable(byte fromTribe, byte toTribe, IReadOnlyList<int> equippedItemIds)
    {
        ArgumentNullException.ThrowIfNull(equippedItemIds);

        for (var i = 0; i < equippedItemIds.Count; i++)
            if (!TryRemapItem(fromTribe, toTribe, equippedItemIds[i], out _))
                return false;

        return true;
    }
}
