namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Term C of the <c>CZ_AUTO_CONFIG_SEND</c> enable (<c>tSort == 1</c>) blocked-zone gate: the level-and-
///     rebirth-gated "battle-zone eligibility" check (legacy <c>CheckLevelBattleZoneNumber</c>,
///     <c>Server/Header/S18_MyZoneInfo.cpp:440-508</c>). <see cref="AutoHuntEnableGate" />'s own remarks
///     explicitly deferred this term pending the full battle-zone map/level-band table ("the remaining
///     battle-zone maps and their per-map level bands... are not covered by any citation opened for this
///     workstream") -- this type supplies that table, sourced from the fully-cited
///     <c>legacy-behavior-translator</c> contract <c>wave13/B11-autoconfig-blocked.md</c>.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/Header/S18_MyZoneInfo.cpp:435-438</c> (composite level = the sum of two internal
///     per-avatar level components -- <see cref="World.PlayerRuntimeState.CombinedLevel" /> in Fenrir, i.e.
///     <c>Level + Level2</c>) and <c>:440-508</c> (the full recognized-server-number switch, its level-band
///     comparison, the four-number rebirth split, the three-number unconditional-true case, and the trailing
///     "unrecognized server number is not blocked by this term" fallback).
///     <para>
///         Server numbers 319, 320, 321 always block under this term regardless of level, and 322/323 block
///         under the same rebirth split as 295/296 below -- but all five are already unconditionally blocked
///         by <see cref="AutoHuntEnableGate" />'s own fixed six-number set (Term A), so this term's own
///         handling of those five numbers is real (implemented faithfully below, matching the contract) but
///         never observably changes the combined outcome. Do not remove them from here on the assumption
///         they're redundant -- the contract is explicit that Term C's own logic still recognizes them.
///     </para>
///     <para>
///         Server number 154's band (<c>Server/Header/S18_MyZoneInfo.cpp</c>, per-zone row cited in the
///         contract) is 1-157 inclusive -- every legally attainable composite level satisfies this band, so
///         this entry is an unconditional block for map 154, not a genuine level restriction. Implemented
///         literally (as a level-band comparison) rather than collapsed to an unconditional <c>true</c>, to
///         stay faithful to the source shape and in case the legal level ceiling ever changes.
///     </para>
///     <para>
///         <b>Deliberately NOT implemented</b>: a build-time configuration flag would, if it were ever
///         active, additionally make server number 295 also block for rebirth-tier-7-or-greater avatars. Per
///         the contract, that flag is never defined in either of the project's two real, shippable build
///         configurations (both force the flag's defining branch to be skipped via a governing macro), so
///         this alternate behavior never actually compiles and is correctly omitted here.
///     </para>
/// </remarks>
public static class AutoHuntBattleZoneEligibilityCatalog
{
    /// <summary>
    ///     Whether the battle-zone eligibility term (Term C) blocks auto-hunt-enable on <paramref name="mapId" />
    ///     for a character with the given composite level (<c>Level + Level2</c>,
    ///     <see cref="World.PlayerRuntimeState.CombinedLevel" />) and rebirth tier
    ///     (<see cref="World.PlayerRuntimeState.RebirthCount" />). A map id recognized by neither this term nor
    ///     <see cref="AutoHuntEnableGate.BlockedMapNumbers" /> (Terms A/B) contributes nothing to blocking.
    /// </summary>
    public static bool IsBlocked(short mapId, int combinedLevel, int rebirthTier) => mapId switch
    {
        49 => combinedLevel is >= 10 and <= 89,
        51 => combinedLevel is >= 20 and <= 29,
        53 => combinedLevel is >= 30 and <= 39,
        120 => combinedLevel is >= 146 and <= 156,
        121 => combinedLevel is >= 150 and <= 153,
        122 => combinedLevel is >= 154 and <= 156,
        146 => combinedLevel is >= 90 and <= 112,
        147 => combinedLevel is >= 50 and <= 59,
        148 => combinedLevel is >= 60 and <= 69,
        149 => combinedLevel is >= 70 and <= 79,
        150 => combinedLevel is >= 80 and <= 89,
        151 => combinedLevel is >= 90 and <= 99,
        152 => combinedLevel is >= 100 and <= 105,
        153 => combinedLevel is >= 106 and <= 112,
        // Every legally attainable level satisfies this band -- an unconditional block for map 154.
        154 => combinedLevel is >= 1 and <= 157,
        155 => combinedLevel is >= 116 and <= 118,
        156 => combinedLevel is >= 119 and <= 121,
        157 => combinedLevel is >= 124 and <= 134,
        158 => combinedLevel is >= 125 and <= 127,
        159 => combinedLevel is >= 128 and <= 130,
        160 => combinedLevel is >= 135 and <= 145,
        161 => combinedLevel is >= 134 and <= 136,
        162 => combinedLevel is >= 137 and <= 139,
        163 => combinedLevel is >= 140 and <= 142,
        164 => combinedLevel is >= 145 and <= 151,
        // Rebirth split: tier >= 7 blocks only 296/323; tier < 7 blocks only 295/322. Level must equal 157.
        // 322/323 are moot in practice (already unconditionally blocked by AutoHuntEnableGate's Term A), but
        // Term C's own logic still recognizes them -- see this type's remarks.
        295 => combinedLevel == 157 && rebirthTier < 7,
        296 => combinedLevel == 157 && rebirthTier >= 7,
        322 => combinedLevel == 157 && rebirthTier < 7,
        323 => combinedLevel == 157 && rebirthTier >= 7,
        // Always blocks regardless of level -- moot in practice (already unconditionally blocked by Term A).
        319 or 320 or 321 => true,
        _ => false
    };
}
