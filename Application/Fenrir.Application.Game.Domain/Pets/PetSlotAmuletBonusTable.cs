using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Pets;

/// <summary>
///     Base Life/Mana bonus table for whichever item currently occupies the <see cref="PetSlots" /> equip
///     slot -- <c>PETSYSTEM::ReturnAmuletLifeValue</c> / <c>ReturnAmuletManaValue</c>. Distinct from, and
///     additive with, the separate "Custom Phoenix Amulet" correction already modeled elsewhere (see the
///     second remarks paragraph below) -- this table is only the base lookup.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:696-774 (<c>ReturnAmuletLifeValue</c>,
///     full per-item-id Life bonus table, <c>iSort==28</c> precondition) ; :776-854
///     (<c>ReturnAmuletManaValue</c>, same precondition and id set) ;
///     Server/Header/Protocol/STRUCT.h:1678-1700 (<c>FITEM_SORT</c>, sort 28 = <c>INEW_PET</c>) ;
///     Server/Header/Protocol/MyFactor.cpp:2140-2143/2317-2320 (<c>GetBaseMaxLife</c>/<c>GetBaseMaxMana</c>
///     consuming this table on <c>aEquip[EPET][0]</c>).
///     <para>
///         <b>Incomplete by design, not by oversight.</b> The originating contract enumerates the full 59-id
///         membership set both legacy switch statements share (<see cref="QualifyingItemIds" />: ranges
///         2151-2154/2174-2189/2195-2206/2253-2254/2261-2262/2301-2302/2410-2421, plus 8290 and 76000-76007) but
///         only transcribes concrete Life/Mana magnitudes for 9 of those 59 ids (76000-76007 and 8290, both in
///         <see cref="ConfirmedBonuses" />). <see cref="GetBaseBonus" /> returns zero for a qualifying id with no
///         confirmed magnitude yet -- treat that zero as "not yet transcribed", not as proof the legacy value is
///         actually zero. A follow-up contract citing the two functions' full bodies is needed before the
///         remaining 50 ids can be added here.
///     </para>
///     <para>
///         <b>RESOLVED -- base and correction STACK additively (workstream pet-amulet-bonus-table-mapping,
///         2026-07-11).</b> Item ids 76005/76006/76007 receive TWO separate, unconditional additions inside
///         <c>GetBaseMaxLife</c>/<c>GetBaseMaxMana</c>: this table's own base amount (this class,
///         5000/7500/12500) PLUS a second, wholly independent "Custom Phoenix Amulet final HP/MP correction"
///         addition (+2000/+4500/+9500, already modeled as <c>StatCalculator.PhoenixFlatBonus</c>). A
///         contiguous re-read of <c>GetBaseMaxLife</c>/<c>GetBaseMaxMana</c> end-to-end
///         (MyFactor.cpp:2120-2224/2280-2357) confirms no early return or conditional branch sits between the
///         base-table call and the correction switch -- they are genuinely sequential and both apply, giving a
///         combined legacy total of 7000/12000/22000 for 76005/76006/76007 respectively, never the base alone,
///         the correction alone, or a replacement of one by the other. The stale "old 3000 -&gt; target N"
///         comments beside the correction switch describe a superseded prior revision of THIS table (which
///         used to return a flat 3000 for these ids before being updated to 5000/7500/12500) and are not
///         evidence of any conditional/exclusivity relationship between the two additions.
///         <c>StatCalculator.ComputeMaxLife</c>/<c>ComputeMaxMana</c> (StatCalculator.Life.cs) now apply the
///         matching two-pass <c>PhoenixFlatBonus(5000, 7500, 12500)</c> then <c>PhoenixFlatBonus(2000, 4500,
///         9500)</c> for the Pet slot, mirroring the shape <c>ComputeDefensePower</c> already used for the
///         identical base magnitudes.
///     </para>
///     <para>
///         <b>Asymmetry, flagged not resolved.</b> This table's own precondition (item resolves AND
///         <c>Sort == </c><see cref="RequiredSortCode" />) has no counterpart in the correction switch, which
///         reads the equipped Pet-slot item id directly with no lookup or sort check at all. An id of
///         76005/76006/76007 that somehow failed this table's own sort==28 gate would still receive the
///         correction addition on its own, decoupled from the base amount -- an observed code asymmetry in the
///         legacy source, not a claim that this state is reachable through normal item granting.
///     </para>
///     <para>
///         Additional citations for the resolution above: Server/ts25zone/GameSystem/GameSystem_07_Pet.cpp:
///         768-770 (base Life values) and :848-850 (base Mana values, identical to Life); Server/Header/
///         Protocol/MyFactor.cpp:1899-1902 (<c>GetBaseMaxLife</c> is not itself macro-gated -- it begins
///         immediately after the enclosing <c>#ifdef __REBIRTH__</c> block's <c>#endif</c>, so it and both
///         additions inside it compile unconditionally in every build variant); :2140 / :2202-2214 (Life
///         base-amount call site and correction switch); :2317 / :2335-2347 (the Mana equivalents);
///         Server/Header/Protocol/STRUCT.h:1662-1676 (<c>FEQUIP_TYPE</c> enum -- confirms <c>EPET</c> is index
///         8, the slot both the base table and the correction switch read via <c>aEquip[8][0]</c>/
///         <c>aEquip[EPET][0]</c>).
///     </para>
/// </remarks>
public static class PetSlotAmuletBonusTable
{
    /// <summary>FITEM_SORT::INEW_PET -- the only sort code this table's precondition accepts.</summary>
    public const byte RequiredSortCode = 28;

    /// <summary>
    ///     The full 59-id membership set both the Life and Mana switch statements share (id coverage only --
    ///     not all of these have a confirmed magnitude in <see cref="ConfirmedBonuses" />, see remarks).
    /// </summary>
    public static readonly ImmutableHashSet<int> QualifyingItemIds = BuildQualifyingIds();

    /// <summary>The 9 of 59 ids whose exact Life/Mana magnitudes were directly confirmed.</summary>
    private static readonly FrozenDictionary<int, (float Life, float Mana)> ConfirmedBonuses =
        new Dictionary<int, (float Life, float Mana)>
        {
            [76000] = (3000f, 3000f),
            [76001] = (3000f, 3000f),
            [76002] = (3000f, 3000f),
            [76003] = (3000f, 3000f),
            [76004] = (3000f, 3000f),
            [76005] = (5000f, 5000f),
            [76006] = (7500f, 7500f),
            [76007] = (12500f, 12500f),
            [8290] = (550f, 500f)
        }.ToFrozenDictionary();

    /// <summary>
    ///     Resolves <paramref name="itemId" />'s base Life/Mana bonus. Returns zero for either of two
    ///     independent reasons the legacy also silently zeroes on: the id isn't in
    ///     <paramref name="itemsById" /> at all, or it resolves but its own catalogued Sort isn't
    ///     <see cref="RequiredSortCode" /> (an id that numerically matches a table entry but isn't Fenrir's
    ///     own sort==28 content is zero here too).
    /// </summary>
    public static (float Life, float Mana) GetBaseBonus(int itemId, FrozenDictionary<int, ItemDefinition> itemsById)
    {
        if (!itemsById.TryGetValue(itemId, out var definition) || definition.Item.Sort != RequiredSortCode)
            return (0f, 0f);

        return ConfirmedBonuses.TryGetValue(itemId, out var bonus) ? bonus : (0f, 0f);
    }

    private static ImmutableHashSet<int> BuildQualifyingIds()
    {
        var builder = ImmutableHashSet.CreateBuilder<int>();
        AddRange(builder, 2151, 2154);
        AddRange(builder, 2174, 2189);
        AddRange(builder, 2195, 2206);
        AddRange(builder, 2253, 2254);
        AddRange(builder, 2261, 2262);
        AddRange(builder, 2301, 2302);
        AddRange(builder, 2410, 2421);
        builder.Add(8290);
        AddRange(builder, 76000, 76007);
        return builder.ToImmutable();
    }

    private static void AddRange(ImmutableHashSet<int>.Builder builder, int lowInclusive, int highInclusive)
    {
        for (var id = lowInclusive; id <= highInclusive; id++) builder.Add(id);
    }
}
