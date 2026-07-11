namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The byte-exact legacy event-code &#8594; <c>WorldInfo</c> state-value tables the ts25center
///     <c>W_ZONE_BROADCAST_FOR_CENTER_SEND</c> handler applies for the numbered zone-siege state machines
///     (<c>Server/ts25center/S04_MyWork02.cpp</c>). These are the mappings <see cref="ZoneCenterBroadcastIngestor" />
///     was previously missing -- it stored the raw event code as the state (an explicit, distinguishable
///     placeholder its own remarks flagged), because this cluster's earlier contract only established the code
///     SETS and the aggregate collapse counts, never the per-code binding. The rvr-siege ingestion behavior
///     contract now supplies every one of these tables literally, so the placeholder is retired.
///     <para>
///         The single most important invariant here (the specific defect being corrected): MANY distinct event
///         codes deliberately map to the SAME state value -- Zone175 codes 69/70/72/76/77/79/83/84/86/90/91/93/
///         97/98/100 all collapse onto state 23, and Zone241 codes 412/413/414 all collapse onto
///         <see cref="DenOfRebirthChallengeState.Ended" /> (a lost challenge and a won one are indistinguishable
///         once stored). Storing the raw event code instead would have made 15 Zone175 states and 2 Zone241
///         states wrong. The mapped state, never the incoming code, is authoritative.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:614-802 (Zone175 two-index table, the 15 codes &#8594; 23 and
///     the 110 reset) ; :929-961 (Zone267 per-slot table) ; :962-986 (Zone241 / Den of Rebirth, codes 411-415
///     &#8594; states {0,1,2}) ; :1132-1150 (Zone335 / FFA single-scalar table). The Zone241 producer-side
///     corroboration (414 on success, 412 on fail, 415 on return-to-town, all writing the same {0,1,2} states) is
///     Server/ts25zone/S07_MyGame01.cpp:11729-11757.
/// </remarks>
public static class SiegeEventStateMap
{
    /// <summary>
    ///     Zone175 (HolyStoneWar) codes 64-100 &#8594; states 1-23. Every code in the 64-100 range maps (there is
    ///     no in-range no-op); code 110's reset-to-0 is handled by the caller's own separate reset branch, and the
    ///     out-of-range no-op codes (101-109, 111, 114, 115, 200) never reach here because the caller gates the
    ///     64-100 range first. Returns <see langword="false" /> defensively for any code outside 64-100.
    /// </summary>
    public static bool TryMapZone175(int eventCode, out int state)
    {
        state = eventCode switch
        {
            64 => 1,
            65 => 2,
            66 => 3,
            67 => 4,
            68 => 5,
            71 => 6,
            73 => 7,
            74 => 8,
            75 => 9,
            78 => 10,
            80 => 11,
            81 => 12,
            82 => 13,
            85 => 14,
            87 => 15,
            88 => 16,
            89 => 17,
            92 => 18,
            94 => 19,
            95 => 20,
            96 => 21,
            99 => 22,
            69 or 70 or 72 or 76 or 77 or 79 or 83 or 84 or 86 or 90 or 91 or 93 or 97 or 98 or 100 => 23,
            _ => -1
        };

        return state >= 0;
    }

    /// <summary>
    ///     Zone267 codes 403-410 &#8594; states {1,2,3,4,5,0}. Code 402 is a no-op (returns
    ///     <see langword="false" />); code 410 resets to state 0 (returns <see langword="true" /> with
    ///     <paramref name="state" /> = 0 -- a real write, distinct from the no-op).
    /// </summary>
    public static bool TryMapZone267(int eventCode, out int state)
    {
        state = eventCode switch
        {
            403 => 1,
            404 => 2,
            405 => 3,
            406 => 5,
            407 => 5,
            408 => 4,
            409 => 5,
            410 => 0,
            _ => -1
        };

        return state >= 0;
    }

    /// <summary>
    ///     Zone335 (FFA) codes 1502-1507 &#8594; states {1,2,3,4,5,0}. Code 1501 is a no-op (the pre-start
    ///     countdown announce, which leaves the shared scalar at 0). Code 1507 resets to state 0 (returns
    ///     <see langword="true" /> with <paramref name="state" /> = 0). There is no separate 1520 reset code in
    ///     the contract -- 1507 IS the reset.
    /// </summary>
    public static bool TryMapZone335(int eventCode, out int state)
    {
        state = eventCode switch
        {
            1502 => 1,
            1503 => 2,
            1504 => 3,
            1505 => 4,
            1506 => 5,
            1507 => 0,
            _ => -1
        };

        return state >= 0;
    }

    /// <summary>
    ///     Zone241 (Den of Rebirth) codes 411-415 &#8594; the three coarse
    ///     <see cref="DenOfRebirthChallengeState" /> values: 411 &#8594; challenge started, 412/413/414 &#8594;
    ///     ended (both failure and success collapse here -- the center-stored state cannot distinguish a won
    ///     challenge from a lost one), 415 &#8594; idle / returned to town. Returns <see langword="false" /> for
    ///     any other code.
    /// </summary>
    public static bool TryMapZone241(int eventCode, out DenOfRebirthChallengeState state)
    {
        switch (eventCode)
        {
            case 411:
                state = DenOfRebirthChallengeState.ChallengeStarted;
                return true;
            case 412:
            case 413:
            case 414:
                state = DenOfRebirthChallengeState.Ended;
                return true;
            case 415:
                state = DenOfRebirthChallengeState.Idle;
                return true;
            default:
                state = DenOfRebirthChallengeState.Idle;
                return false;
        }
    }
}
