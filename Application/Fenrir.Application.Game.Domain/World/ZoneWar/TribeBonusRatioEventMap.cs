using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Sub-mechanic A of the C6-zone241-bonus behavior contract -- the tribe-wide bonus-ratio sub-selector
///     event ts25center's <c>W_ZONE_BROADCAST_FOR_CENTER_SEND</c> handler dispatches on <c>tSort=301</c>
///     (<c>Server/ts25center/S04_MyWork02.cpp:854-920</c>). Despite that block's own banner comment reading
///     "ZONE 194 BONUS," it has no relationship to <see cref="ZoneCenterSiegeState.SetZone241" />/the Zone241
///     "Den of Rebirth" family (sub-mechanic B of the same contract, already bound via
///     <see cref="SiegeEventStateMap.TryMapZone241" />) -- this class writes four separate per-tribe arrays
///     entirely of its own.
///     <para>
///         This closes GAP 2 called out in <see cref="ZoneCenterBroadcastIngestor" />'s own class remarks: the
///         event code (301) and the payload's inner <c>tEventSort</c> sub-selector encoding (sixteen codes,
///         21-24/31-34/41-44/51-54, one per tribe x four bonus families) were both previously uncited; the
///         C6-zone241-bonus contract now supplies both, so <see cref="ZoneCenterSiegeState" />'s four
///         already-implemented setters (<see cref="ZoneCenterSiegeState.SetExperienceBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetItemDropBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetMyoungItemDropBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetKillOtherTribeBonus" />) finally have a caller.
///     </para>
///     <para>
///         Payload shape (offsets into the 130-byte <c>tData</c> buffer,
///         <c>Server/Header/Protocol/DEFINE.h:413</c> for the 4-byte <c>_int</c> stride): <c>tEventSort</c> at
///         offset 0, <c>tEventValue</c> (signed) at offset 4 -- both read via <c>CopyMemory</c> in the legacy
///         source (<c>S04_MyWork02.cpp:855-856</c>).
///     </para>
///     <para>
///         Scaling: for the three ratio families (general-experience-up, item-drop-up, item-drop-up-for-Myoung
///         -- <c>tEventSort</c> 21-24/31-34/41-44), the stored value is <c>tEventValue * 0.1f</c>, a straight
///         decile scale (<c>S04_MyWork02.cpp:860-893</c>). For the kill-other-tribe-add-value family
///         (<c>tEventSort</c> 51-54), <c>tEventValue</c> is stored verbatim with no scaling, AND the other
///         three tribes' slots in that same array are reset to zero in the same write -- a mutually-exclusive
///         family, unlike the three ratio families above which never touch another tribe's slot
///         (<c>S04_MyWork02.cpp:895-918</c>). <see cref="ZoneCenterSiegeState.SetKillOtherTribeBonus" /> already
///         implements that zero-the-other-three-tribes step; this class only resolves which tribe index to pass
///         it.
///     </para>
///     <para>
///         Tribe index derivation: each family spans exactly <see cref="WorldStateService.TribeCount" /> (4)
///         contiguous codes, one per tribe, e.g. 21&#8594;tribe 0, 22&#8594;tribe 1, 23&#8594;tribe 2,
///         24&#8594;tribe 3 -- every one of the sixteen recognized values maps to a literal, hardcoded array
///         index in the legacy source itself (verified end-to-end by the contract's own citation,
///         <c>S04_MyWork02.cpp:860-917</c>), so the arithmetic derivation here (<c>tEventSort - familyBase</c>)
///         reproduces exactly those sixteen literal mappings and can never itself compute an out-of-range index
///         given the range-gated <c>switch</c> below.
///     </para>
///     <para>
///         Unrecognized <c>tEventSort</c>: the legacy inner selector has no <c>default:</c> case
///         (<c>S04_MyWork02.cpp:857-919</c>) -- an unrecognized code writes nothing to any of the four arrays.
///         The caller (<see cref="ZoneCenterBroadcastIngestor" />) still relays the raw
///         <c>(eventCode, data)</c> pair to every other zone unconditionally regardless of whether this method
///         recognized it -- see that class's own generic post-switch fallthrough, which this class has no part
///         in and does not need to reproduce here.
///     </para>
///     <para>
///         NOT reproduced here, by contract-flagged design (see the C6-zone241-bonus contract's own Open
///         questions -- do not silently "fix" either of these without a fresh legacy-behavior-translator pass):
///     </para>
///     <list type="bullet">
///         <item>
///             The reset-asymmetry gap: the originating zone sends <c>tSort=3001</c> to locally zero its own
///             four tribe arrays before a tribe war begins (<c>S07_MyGame01.cpp:10188-10196</c>), but
///             ts25center's outer switch never had a <c>case 3001:</c> at all -- meaning the legacy center's
///             own copy (and, by extension, this class) is only ever SET via <c>tSort=301</c>, never reset by
///             that signal. Reproducing or closing this gap is an explicit product decision left to whoever
///             wires a Fenrir-side equivalent of the pre-battle zeroing broadcast, not assumed here.
///         </item>
///         <item>
///             No symbolic tribe names for indices 0-3 were recoverable from the cited files -- callers needing
///             human-readable tribe names must source that mapping elsewhere (client resources or a separate
///             ServerDocs campaign), not from this class.
///         </item>
///     </list>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:851-920 (full <c>case 301</c> block: the inner
///     <c>tEventSort</c> selector, all sixteen recognized codes, the 51-54 mutual-exclusion zeroing) ;
///     Server/Header/Protocol/STRUCT.h:619-622 (the four tribe arrays' field declarations) ;
///     Server/Header/Protocol/DEFINE.h:309,413 (<c>MAX_TRIBE_NUM=4</c> ; <c>_int</c> = 4-byte stride) ;
///     Server/ts25zone/S07_MyGame01.cpp:3364-3398 (<c>MyGame::ProcessForWin194</c>, the one live production
///     sender) ; Server/ts25zone/S07_MyGame01.cpp:10495-10546 (the one live call site, <c>tEventValue</c>
///     hardcoded to 1 in every branch).
/// </remarks>
public static class TribeBonusRatioEventMap
{
    /// <summary>
    ///     <c>tSort</c> value this whole sub-mechanic is dispatched under
    ///     (<c>Server/ts25center/S04_MyWork02.cpp:212,851</c>). Wire this into
    ///     <see cref="ZoneCenterBroadcastIngestor" />'s own <c>ApplyStateEffect</c> range table -- see that
    ///     class's own remarks, GAP 2, and this slice's reported wiring manifest.
    /// </summary>
    public const int EventCode = 301;

    private const int GeneralExperienceUpRatioFamilyBase = 21;
    private const int ItemDropUpRatioFamilyBase = 31;
    private const int ItemDropUpRatioForMyoungFamilyBase = 41;
    private const int KillOtherTribeAddValueFamilyBase = 51;

    /// <summary>Straight decile scale applied to the three ratio families' raw wire value (never the kill-bonus family).</summary>
    private const float RatioScale = 0.1f;

    /// <summary>
    ///     Decode a <c>tSort=301</c> payload and apply whichever one of the four per-tribe arrays it targets.
    ///     Never throws for an unrecognized <c>tEventSort</c> -- matches the legacy inner selector's own
    ///     no-<c>default:</c>-case, silently-ignored behavior for that case; the caller's generic relay to
    ///     every other zone happens regardless and is not this method's concern.
    /// </summary>
    /// <param name="state">The process-wide siege/world-info mirror to mutate.</param>
    /// <param name="data">
    ///     The raw 130-byte broadcast payload (<see cref="ZoneCenterBroadcastIngestor.PayloadSize" />) -- only
    ///     the first 8 bytes (<c>tEventSort</c>, <c>tEventValue</c>) are read.
    /// </param>
    /// <param name="logger">Optional -- used only to record an unrecognized <c>tEventSort</c> at Debug level.</param>
    public static void Apply(ZoneCenterSiegeState state, ReadOnlySpan<byte> data, ILogger? logger = null)
    {
        var eventSort = ReadInt32(data, 0);
        var eventValue = ReadInt32(data, 4);

        switch (eventSort)
        {
            case >= GeneralExperienceUpRatioFamilyBase
                and < GeneralExperienceUpRatioFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - GeneralExperienceUpRatioFamilyBase);
                state.SetExperienceBonusRatio(tribeIndex, eventValue * RatioScale);
                break;
            }

            case >= ItemDropUpRatioFamilyBase and < ItemDropUpRatioFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - ItemDropUpRatioFamilyBase);
                state.SetItemDropBonusRatio(tribeIndex, eventValue * RatioScale);
                break;
            }

            case >= ItemDropUpRatioForMyoungFamilyBase
                and < ItemDropUpRatioForMyoungFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - ItemDropUpRatioForMyoungFamilyBase);
                state.SetMyoungItemDropBonusRatio(tribeIndex, eventValue * RatioScale);
                break;
            }

            case >= KillOtherTribeAddValueFamilyBase
                and < KillOtherTribeAddValueFamilyBase + WorldStateService.TribeCount:
            {
                var tribeIndex = (byte)(eventSort - KillOtherTribeAddValueFamilyBase);
                // Verbatim, no scaling -- and SetKillOtherTribeBonus already atomically zeroes the other three
                // tribes' slots in the same write (S04_MyWork02.cpp:895-918).
                state.SetKillOtherTribeBonus(tribeIndex, eventValue);
                break;
            }

            default:
                // No default case in the legacy inner selector -- nothing is written for an unrecognized code.
                logger?.LogDebug(
                    "Tribe bonus-ratio event (tSort=301) carried unrecognized tEventSort {EventSort} -- ignored, matching legacy's own missing default case",
                    eventSort);
                break;
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
