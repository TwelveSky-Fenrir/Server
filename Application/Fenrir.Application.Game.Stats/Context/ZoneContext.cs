namespace Fenrir.Application.Game.Stats.Context;

/// <summary>
///     Zone-context stat inputs the legacy <c>MyFactor</c> reads that the current equipment-only
///     <see cref="StatCalculator" /> surface has no channel for: the current zone number and the state that is
///     gated by it (ornament, elixir/official-guild "B4G" override, rage gauge, rank-buff tier, tribe role,
///     drunk state). Every field defaults to a neutral/zero value, so a <c>default</c> instance reproduces
///     "no zone-context contribution". <b>This context deliberately straddles both layers</b> (see the
///     per-field notes): rage, the ornament HP/MP/DMG/DEF bonus and the HP rank-buff tier feed the BASE cache,
///     while tribe-role, drunk and the remaining rank-buff tiers feed the per-resolution EFFECTIVE layer -- a
///     consuming workstream must preserve that split (a base input changing needs a recompute event, an
///     effective input is read live).
/// </summary>
/// <remarks>
///     Introduced by workstream B1 as a pure plumbing seam: threaded through
///     <see cref="StatCalculator.ComputeBaseStats" />/<see cref="StatCalculator.ComputeEffectiveStats" /> but
///     not read by any formula yet (each contributes 0). Réf. legacy input surface: ornament
///     <c>Server/Header/Protocol/MyFactor.cpp:513-547</c> (IsUsedOrnament + fixed gold/silver bonuses); elixir
///     and official-guild "B4G" override <c>MyFactor.cpp:560-731</c>; rage <c>MyFactor.cpp:2615</c>
///     (aRageGauge via ReturnRageBuffRate scaling BASE attack power); tribe-role/drunk/rank-buff effective
///     applications <c>MyFactor.cpp:4009-4143</c>; zone-number gating and the equalized-stats event-zone
///     override <c>MyFactor.cpp:58-62,1712-1717</c>.
/// </remarks>
/// <param name="ZoneNumber">
///     The current map/zone number (<c>PlayerRuntimeState.MapId</c>). Multiple bonuses are suppressed inside
///     one specific zone, the elixir path has its own zone-eligibility range, and one event zone short-circuits
///     every stat to a fixed table -- so any zone-context resolution needs this, not just the buff states.
/// </param>
/// <param name="OrnamentInUse">aUseOrnament -- the ornament-in-use flag gating the fixed HP/MP/DMG/DEF bonus (BASE).</param>
/// <param name="OrnamentGoldTimeRemaining">
///     Remaining gold-tier ornament time counter -- selects the higher (gold) fixed bonus tier (BASE). No
///     <c>PlayerRuntimeState</c> source field exists yet; stays 0 until one lands.
/// </param>
/// <param name="OrnamentSilverTimeRemaining">
///     Remaining silver-tier ornament time counter -- selects the lower (silver) fixed bonus tier (BASE). Same
///     "no source field yet" note as <paramref name="OrnamentGoldTimeRemaining" />.
/// </param>
/// <param name="RankBuffType">
///     aRankBuffType (1-7, 0 = none, <c>PlayerRuntimeState.RankBuffType</c>) -- selects which single stat
///     receives the rank-buff bonus. One tier feeds a BASE HP bonus; the remaining tiers apply in the EFFECTIVE
///     layer.
/// </param>
/// <param name="TribeRole">
///     ReturnTribeRole (0 regular / 1 master / 2 sub-master / 3 council, <c>PlayerRuntimeState.TribeRole</c>) --
///     master/sub-master add a flat EFFECTIVE bonus to attack and defense, gated on not being in one excluded
///     zone.
/// </param>
/// <param name="RageGauge">
///     aRageGauge -- scales <b>BASE</b> attack power through the rage-buff-rate lookup (note: this is a base
///     input despite the source finding grouping rage with the zone-context/buff family). No
///     <c>PlayerRuntimeState</c> source field exists yet; stays 0 until one lands.
/// </param>
/// <param name="DrunkStateId">
///     GetDrukValue drunk/"Druk" identifier (0 = not drunk) -- several ids apply per-stat, non-uniform
///     EFFECTIVE modifiers. No <c>PlayerRuntimeState</c> source field exists yet; stays 0 until one lands.
/// </param>
/// <param name="GuildBuffActive">
///     The live guild-buff on/off gate (<c>PlayerRuntimeState.GuildBuffActive</c>) -- one of the inputs the
///     official-guild/elixir override path consults.
/// </param>
/// <param name="GuildId">
///     This character's guild id (0 = none, <c>PlayerRuntimeState.GuildId</c>) -- used to resolve whether the
///     guild is one of its tribe's four official ("B4G") guilds, which overrides the per-elixir bonus with
///     fixed values. The official-guild membership table itself is not carried here.
/// </param>
public readonly record struct ZoneContext(
    short ZoneNumber = 0,
    bool OrnamentInUse = false,
    int OrnamentGoldTimeRemaining = 0,
    int OrnamentSilverTimeRemaining = 0,
    int RankBuffType = 0,
    byte TribeRole = 0,
    int RageGauge = 0,
    int DrunkStateId = 0,
    bool GuildBuffActive = false,
    int GuildId = 0);
