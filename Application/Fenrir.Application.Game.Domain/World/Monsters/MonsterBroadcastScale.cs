namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Legacy per-monster-type AOI broadcast-radius scale, resolved once from the monster's own template
///     columns and reused for every state broadcast that monster makes.
/// </summary>
/// <remarks>
///     Server/ts25zone/S10_MySummon.cpp:612-647 (<c>MySummon::ReturnSpecialSortNumber</c>) maps
///     <c>MONSTER_INFO.mType</c>/<c>mSpecialType</c> (<see cref="MonsterRowDto.Type" />/
///     <see cref="MonsterRowDto.SpecialType" />) to a "special sort number" (1/2/3/4/5/6/10),
///     assigned once per spawned instance (<c>mon-&gt;mSpecialSortNumber</c>, S10_MySummon.cpp:806).
///     Server/ts25zone/S07_MyGame05.cpp:3982-4001 (<c>MONSTER_OBJECT::SendSpecialNumber</c>) maps that special
///     sort number to <c>Send1</c>/<c>Send2</c>/<c>Send3</c>, which call <c>Broadcast11</c> with
///     <c>UNIT_SCALE_RADIUS1</c>/<c>2</c>/<c>3</c> respectively (S07_MyGame05.cpp:3967-3980,
///     Server/Header/Protocol/DEFINE.h:141-143). The periodic monster catch-up tick -- the one broadcast family
///     with an unambiguous, single call site for this dispatch -- always resolves scale this same way
///     (<c>tMONSTER_OBJECT-&gt;SendSpecialNumber()</c>, Server/ts25zone/S07_MyGame01.cpp:2566).
///     <para>
///         Resolved mapping (every case independently re-derived from the two switches above, not assumed):
///         <c>mType == 1</c> with <c>mSpecialType</c> in {11,12,13,14,15,28} -&gt; scale 2 (special sort 5,
///         <c>Send2</c>); <c>mType == 1</c> with <c>mSpecialType</c> in {18,21,22,23,29,31,32,33,34,35,36,37,38}
///         -&gt; scale 3 (special sorts 2/3/4/6, <c>Send3</c>); <c>mType</c> in {6,7,8,9} -&gt; scale 2 (special
///         sort 5, <c>Send2</c>); every other combination, INCLUDING <c>mType == 1</c> with
///         <c>mSpecialType == 10</c> (the one special-type explicitly commented "Tower" in the source,
///         S10_MySummon.cpp:619) -&gt; scale 1 (special sorts 1/10, both dispatched to <c>Send1</c>,
///         S07_MyGame05.cpp:3987-3999). The Tower guardian case is deliberately called out: it resolves to its
///         own dedicated special sort (10) but that sort's own <c>SendSpecialNumber</c> case still calls
///         <c>Send1</c> (scale 1), not a wider radius -- confirmed by reading both switches together rather than
///         assumed from the dedicated case existing at all.
///     </para>
///     <para>
///         The monster-SPAWN announcement (<see cref="Zone.SpawnMonster" />) reuses this same mapping for
///         simplicity, even though legacy lets a spawn call site choose a different, fixed dispatch instead
///         (<c>ESEND_MONSTER_1/2/3</c> -&gt; always <c>Send1</c>/<c>Send3</c>/<c>Send3</c> regardless of this
///         monster's own special sort, or <c>ESEND_MONSTER_SPECIAL_TYPE</c> -&gt; a differently-keyed
///         <c>SendSpecialType()</c> dispatch, Server/ts25zone/S10_MySummon.cpp:854-871) -- which of those several
///         strategies fires varies per specific summon call site and was not modeled call-site-by-call-site here.
///         Applying this class's own mapping (one of legacy's own valid strategies,
///         <c>ESEND_MONSTER_SPECIAL_NUMBER</c>) to every <see cref="Zone.BroadcastMonsterAction" /> call --
///         spawn included -- is a deliberate simplification consistent with Fenrir's single shared broadcast
///         helper, not a re-guess of the still-open per-call-site question.
///     </para>
/// </remarks>
public static class MonsterBroadcastScale
{
    /// <summary>
    ///     The legacy <c>Broadcast11</c>/<c>Broadcast22</c> <c>iScale</c> this monster's own state broadcasts
    ///     use (1, 2, or 3) -- see class remarks for the full derivation.
    /// </summary>
    public static int ForMonster(byte monsterType, byte monsterSpecialType)
    {
        if (monsterType == 1)
            return monsterSpecialType switch
            {
                11 or 12 or 13 or 14 or 15 or 28 => 2,
                18 or 21 or 22 or 23 or 29 or 31 or 32 or 33 or 34 or 35 or 36 or 37 or 38 => 3,
                _ => 1 // includes special type 10 (Tower guardian) and every other unlisted mSpecialType
            };

        return monsterType is 6 or 7 or 8 or 9 ? 2 : 1;
    }
}
