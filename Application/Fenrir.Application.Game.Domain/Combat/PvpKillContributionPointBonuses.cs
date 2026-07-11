using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     The per-kill CP-amount terms of <c>MyUtil::ProcessForKillOtherTribe</c>
///     (<c>Server/ts25zone/S07_MyGame03.cpp:2664-2713</c>) that <see cref="PvpKillContributionPointCalculator" />
///     left un-computed because the earlier finding did not state their exact trigger conditions. The B9 contract
///     now supplies them. This type <b>extends</b> that calculator rather than duplicating it: the premium (+2),
///     warrior-scroll (+1) and base-composition (game-wide add + per-user add + per-tribe world + per-tribe
///     tower) terms already live on <see cref="PvpKillContributionPointCalculator.ComputeBaseAmount" />; this
///     type only adds the game-wide/per-user derivations and the three conditional additive bonuses that were
///     previously omitted. Pure arithmetic, no I/O, no state.
/// </summary>
/// <remarks>
///     <para>
///         <b>Base composition</b> (<c>S07_MyGame03.cpp:2664-2668</c>): the base CP amount is the sum of the
///         game-wide cross-tribe add value (<see cref="ComputeGameWideAddValue" />), the per-user cross-tribe add
///         value (<see cref="ComputePerUserAddValue" />), a per-tribe world CP bonus, and a per-tribe tower-war CP
///         bonus. The last two are runtime/event-driven and NOT recoverable from the contract -- they remain the
///         caller-supplied <c>perTribeWorldStateBonus</c>/<c>towerControlBonus</c> parameters
///         <see cref="PvpKillContributionPointCalculator.ComputeBaseAmount" /> already exposes.
///     </para>
///     <para>
///         <b>Not recoverable, never invented</b>: the game-wide cross-tribe add value's own configured magnitude
///         (shipped BuildEU33 value 3, doubled to an effective 6 by the always-active rebirth build macro -- so
///         pass the raw config value to <see cref="ComputeGameWideAddValue" />, which applies the doubling); and
///         the per-server "home tribe" mapping the minority-capital bonus compares against. The latter is not
///         given by the contract at all, so <see cref="ComputeConditionalBonuses" /> takes it as
///         <c>serverHomeTribe</c> and withholds the bonus entirely when it is left at -1 rather than guessing the
///         four-server-to-tribe assignment.
///     </para>
/// </remarks>
public static class PvpKillContributionPointBonuses
{
    /// <summary>
    ///     The game-wide cross-tribe add value is doubled because the rebirth build macro (<c>__REBIRTH__</c>) is
    ///     unconditionally active (<c>S07_MyGame01.cpp:447-450</c>).
    /// </summary>
    public const int RebirthAddValueMultiplier = 2;

    /// <summary>
    ///     The per-user cross-tribe add value is +1 when the attacker's active time-effect equals the
    ///     cross-tribe-add effect code (300), otherwise 0 (<c>S04_MyWork02.cpp:530,537</c>).
    /// </summary>
    public const int PerUserCrossTribeAddBonus = 1;

    /// <summary>
    ///     +2 on server 160 when the attacker's tribe equals the runtime "added-CP" tribe
    ///     (<c>S07_MyGame03.cpp:2670-2713</c>).
    /// </summary>
    public const int Server160AddedTribeBonus = 2;

    /// <summary>
    ///     +10 when the attacker's BASE level is at least <see cref="SymbolBattleMinimumBaseLevel" /> and the
    ///     server is <see cref="SymbolBattleServerId" /> (38) with symbol battle active
    ///     (<c>S07_MyGame03.cpp:2670-2713</c>).
    /// </summary>
    public const int SymbolBattleBaseLevelBonus = 10;

    /// <summary>
    ///     +10 "minority-capital" on servers 1/6/11/140 for any tribe that is not that server's home tribe
    ///     (<c>S07_MyGame03.cpp:2670-2713</c>).
    /// </summary>
    public const int MinorityCapitalBonus = 10;

    /// <summary>
    ///     The BASE level (Level field only, never the combined Level+Level2) at/above which the symbol-battle CP
    ///     bonus applies (<c>Server/Header/Protocol/DEFINE.h:473</c>).
    /// </summary>
    public const int SymbolBattleMinimumBaseLevel = 135;

    /// <summary>The server number gated by the symbol-battle +10 bonus.</summary>
    public const short SymbolBattleServerId = 38;

    /// <summary>The server number gated by the added-CP-tribe +2 bonus.</summary>
    public const short AddedCpTribeServerId = 160;

    private static readonly FrozenSet<short> MinorityCapitalServerIds = new short[] { 1, 6, 11, 140 }.ToFrozenSet();

    /// <summary>
    ///     The game-wide cross-tribe add value: <paramref name="crossTribeCpAddValueConfig" /> (the raw
    ///     deployment-configured value) doubled by <see cref="RebirthAddValueMultiplier" />. Feeds the
    ///     <c>basePerKillAmount</c> parameter of
    ///     <see cref="PvpKillContributionPointCalculator.ComputeBaseAmount" />.
    /// </summary>
    public static int ComputeGameWideAddValue(int crossTribeCpAddValueConfig)
    {
        return crossTribeCpAddValueConfig * RebirthAddValueMultiplier;
    }

    /// <summary>
    ///     The per-user cross-tribe add value: <see cref="PerUserCrossTribeAddBonus" /> (1) when
    ///     <paramref name="hasCrossTribeAddTimeEffect" /> is set, otherwise 0. Feeds the
    ///     <c>perCharacterOverride</c> parameter of
    ///     <see cref="PvpKillContributionPointCalculator.ComputeBaseAmount" />.
    /// </summary>
    public static int ComputePerUserAddValue(bool hasCrossTribeAddTimeEffect)
    {
        return hasCrossTribeAddTimeEffect ? PerUserCrossTribeAddBonus : 0;
    }

    /// <summary>
    ///     The sum of the three server/tribe/level-conditional additive CP bonuses, layered on top of the base
    ///     amount (which already includes premium/warrior-scroll). At most one can fire for a given kill since
    ///     each is gated on a distinct, mutually-exclusive <paramref name="mapId" />.
    /// </summary>
    /// <param name="mapId">The map/server number the kill happened on.</param>
    /// <param name="attackerTribe">The attacker's tribe.</param>
    /// <param name="addedCpTribe">
    ///     The runtime "added-CP" tribe (<c>mTribeAddCP</c>, initialized to -1 and only ever changed by event
    ///     logic elsewhere). A negative value means no tribe is currently designated, so the server-160 bonus
    ///     never fires.
    /// </param>
    /// <param name="attackerBaseLevel">The attacker's BASE level (Level field only, not the combined level).</param>
    /// <param name="symbolBattleActive">Whether symbol battle is currently active (gates the server-38 bonus).</param>
    /// <param name="serverHomeTribe">
    ///     The home tribe of a minority-capital server (1/6/11/140). The per-server-to-tribe mapping is not
    ///     recoverable from the B9 contract; leave at -1 to withhold the minority-capital bonus rather than
    ///     invent it.
    /// </param>
    public static int ComputeConditionalBonuses(
        short mapId,
        int attackerTribe,
        int addedCpTribe,
        int attackerBaseLevel,
        bool symbolBattleActive,
        int serverHomeTribe = -1)
    {
        var bonus = 0;

        if (mapId == AddedCpTribeServerId && addedCpTribe >= 0 && attackerTribe == addedCpTribe)
            bonus += Server160AddedTribeBonus;

        if (mapId == SymbolBattleServerId && symbolBattleActive
                                          && attackerBaseLevel >= SymbolBattleMinimumBaseLevel)
            bonus += SymbolBattleBaseLevelBonus;

        if (serverHomeTribe >= 0 && MinorityCapitalServerIds.Contains(mapId) && attackerTribe != serverHomeTribe)
            bonus += MinorityCapitalBonus;

        return bonus;
    }
}
