using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     CZ_FISHING_RESULT_SEND (opcode 104)'s second, non-client-initiated trigger: once per game tick, for
///     every avatar currently on the zone-52 fishing shard whose fishing session is active and still at step
///     2 ("waiting to bite", set by a successful cast -- <c>FishingLineService.Cast</c>), advances the step
///     to 3 ("ready to bite") once at least one real-world minute has elapsed since the cast timestamp, and
///     pushes the same acknowledgment shape as <c>FishingProgressResponse</c> (sub-action hardcoded to 3,
///     current fishing state, new step) to that player only. Without this, <c>FishingProgressService.PollBite</c>
///     (sub-action 1, "poll for a bite") can never do anything -- its own gate explicitly requires
///     <c>FishingStep == 3</c>, which no other production code path ever sets.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:1316-1325 (the per-tick, server-52-only step-2-to-step-3
///     auto transition) ; Server/ts25zone/S04_MyWork01.cpp:115-122 (shard-52-only dispatch registration for
///     all three fishing opcodes, under the unconditionally-active <c>USE_FISHING</c>,
///     Server/Header/Protocol/DEFINE.h:147) ; Server/Header/Protocol/DEFINE.h:215 (<c>TICK_01_MINUTE</c> =
///     60,000 ms).
///     <para>
///         This is a DISTINCT one-minute window from the one gating sub-action 1's own bite roll inside
///         <c>FishingProgressService.PollBite</c> -- both reuse the same tick-count field in the legacy, but
///         are stamped at different points (casting stamps the wait-for-step-3 timer,
///         <c>FishingLineService.Cast</c>; re-baiting stamps the bite-roll timer,
///         <c>FishingProgressService.Recast</c>), so the two windows are sequential, never concurrent, per
///         the FishingProgress behavior contract.
///     </para>
///     <para>
///         No animation broadcast accompanies this transition (unlike reaching step 4/5, which broadcasts to
///         self + AOI neighbors via <c>Zone.ApplyFishingCommand</c>) -- the legacy handler only ever
///         broadcasts on the catch/miss branch, never on this one, per the FishingProgress behavior
///         contract's own Outputs section.
///     </para>
/// </remarks>
public sealed class FishingBiteWindowSystem : ISimulationSystem
{
    /// <summary>
    ///     Legacy zone/server number 52 -- Server/ts25zone/S04_MyWork01.cpp:115-122, the same shard gate
    ///     <c>FishingLineHandler</c>/<c>FishingProgressHandler</c> enforce on the client-request side.
    /// </summary>
    public const short FishingZoneNumber = 52;

    /// <summary>
    ///     ZC_FISHING_RESULT_RECV's sub-action field, hardcoded to 3 for this server-driven push -- same wire
    ///     value <c>FishingProgressService.ForceStep</c> sends for client sub-action 3 (the acknowledgment
    ///     shape is identical; only the trigger differs), per the FishingProgress behavior contract's own
    ///     "sub-action value hardcoded to 3" wording for this exact background transition.
    /// </summary>
    private const int BiteWindowArmedResultSort = 3;

    private static readonly TimeSpan BiteWindowDelay = TimeSpan.FromMinutes(1);

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (zone.MapId != FishingZoneNumber)
            return;

        var now = DateTime.UtcNow;

        foreach (var state in zone.Players)
        {
            if (state.FishingState == 0 || state.FishingStep != 2 || state.FishingCastAtUtc is not { } castAt ||
                now - castAt < BiteWindowDelay)
                continue;

            state.FishingStep = 3;

            state.Session.Send(new FishingProgressResponse
            {
                ServerIndex = state.CharacterId,
                UniqueNumber = state.UniqueNumber,
                Result = BiteWindowArmedResultSort,
                FishingState = state.FishingState,
                FishingStep = state.FishingStep
            });
        }
    }
}
