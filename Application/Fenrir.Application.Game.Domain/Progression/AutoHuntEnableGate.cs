using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     The <c>CZ_AUTO_CONFIG_SEND</c> enable (<c>tSort == 1</c>) blocked-zone gate
///     (<c>Server/ts25zone/S04_MyWork02.cpp:13540-13553</c>): enabling auto-hunt on one of these maps is refused
///     and the session disconnected. Centralises and freezes the fixed map-number set that was previously an
///     inline <see cref="HashSet{T}" /> in <c>AutoHuntToggleService</c>, as a boot-frozen
///     <see cref="FrozenSet{T}" /> read lock-free for the process lifetime (same posture as every other
///     frozen-at-boot lookup in this codebase).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:13540-13553 -- refuses server number 38, 319-323, any
///     <c>CheckZone241Type</c> server, and any map the level-gated battle-zone eligibility check
///     (<c>CheckLevelBattleZoneNumber</c>) says the character qualifies to fight in. Fenrir shards one map per
///     process, so a "server number" maps to a map id; the legacy "zone 241 type" flag is 20 distinct server
///     numbers (241-249, 292-294, 311-312, 325-330), all included here, matching the set the shipping
///     <c>AutoHuntToggleService</c> already refused.
///     <para>
///         The level-gated battle-zone eligibility term (<c>CheckLevelBattleZoneNumber</c>,
///         <c>Server/Header/S18_MyZoneInfo.cpp:440-508</c>) that was previously deferred here is now implemented
///         separately in <see cref="AutoHuntBattleZoneEligibilityCatalog" /> (contract
///         <c>wave13/B11-autoconfig-blocked.md</c>) -- see that type for the full battle-zone map/level-band
///         table, the 295/296/322/323 rebirth split, and the 319-321 unconditional-block case. Callers must OR
///         this gate's <see cref="IsEnableBlocked" /> together with
///         <see cref="AutoHuntBattleZoneEligibilityCatalog.IsBlocked" /> to get the full three-term gate --
///         see <c>AutoHuntToggleService.ToggleAsync</c> for the combined call.
///     </para>
/// </remarks>
public static class AutoHuntEnableGate
{
    /// <summary>
    ///     The cited fixed map-number blocks: 38, 319-323, and the 20 "zone 241 type" server numbers
    ///     (<c>S04_MyWork02.cpp:13540-13553</c>).
    /// </summary>
    public static readonly FrozenSet<short> BlockedMapNumbers = new short[]
    {
        38, 319, 320, 321, 322, 323,
        241, 242, 243, 244, 245, 246, 247, 248, 249,
        292, 293, 294,
        311, 312,
        325, 326, 327, 328, 329, 330
    }.ToFrozenSet();

    /// <summary>
    ///     Whether enabling auto-hunt on <paramref name="mapId" /> is refused by the cited fixed-number blocks
    ///     (Terms A/B). Does NOT include the level-gated battle-zone term (Term C) -- see
    ///     <see cref="AutoHuntBattleZoneEligibilityCatalog" /> and this type's own remarks for the combined gate.
    /// </summary>
    public static bool IsEnableBlocked(short mapId)
    {
        return BlockedMapNumbers.Contains(mapId);
    }
}
