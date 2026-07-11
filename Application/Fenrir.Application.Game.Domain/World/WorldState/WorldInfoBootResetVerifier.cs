using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     A5-worldinfo-boot-reset: the explicit, auditable boot-time step confirming Fenrir's own process-wide
///     WORLD_INFO-equivalent singletons for DTM (<see cref="ZoneCenterSiegeState" />), Nok-San
///     (<see cref="Zone195NokSanState" />), popup (<see cref="PopupEventState" />), and alliance-possibility
///     (<see cref="AllianceProposalCenterState" />) actually start at the legacy compiled-in reset value
///     (Server/ts25center/S07_MyGame01.cpp:36-47,127-144) the moment this step runs -- matching legacy's own
///     explicit, unconditional reset call rather than silently relying on "a freshly constructed DI singleton
///     happens to zero-initialize its backing arrays".
///     <para>
///         Today, every one of the four singletons this class reads is ALWAYS already at its correct
///         boot-reset value the first time this runs, because none of them has any persisted-load path that
///         could populate them with stale data before this step executes (each is a plain DI singleton built
///         fresh, with zero-initialized backing arrays, on process start -- see each class's own remarks). This
///         method's value is therefore a REGRESSION TRIPWIRE, not a live corrective action: if a future change
///         ever gives one of these types a persisted-load path, reuses an instance across some kind of
///         soft-restart, or otherwise lets non-zero state reach this point before the first zone tick or the
///         first accepted connection, this step surfaces that violation loudly (<see cref="LogLevel.Error" />)
///         at boot instead of it silently corrupting the very first RvR/Nok-San/popup/alliance broadcast a
///         player receives.
///     </para>
///     <para>
///         Deliberately does not throw on a violation: unlike <c>ShardPartitionGuard</c>'s hard
///         resource-ownership conflict (two shards claiming the same map), a nonzero value here signals a latent
///         Fenrir-internal bug in how one of these singletons gets constructed/reused, not a data-corrupting
///         invariant this process could actively make worse by continuing to boot -- degrade-and-warn is the
///         right posture (see the realtime-simulation-architect's own "shard invariant" taxonomy), matching how
///         legacy's own reset failure paths (contract Error/failure semantics) are logged rather than crashing
///         the process outright.
///     </para>
///     <para>
///         Alliance-possibility (<c>mPossibleAllianceInfo</c>/<c>mAllianceState</c>, contract Side effects steps
///         3-4) IS verified, via the sibling A5-alliance-proposal slice's own new
///         <see cref="AllianceProposalCenterState" /> -- its backing arrays (<c>byte?[,]</c> for the two formed
///         alliance slots, an <see cref="AlliancePossibleInfo" /> per tribe) already default, at construction, to
///         exactly the legacy compiled-in reset ("empty" == both cells null; the proposal-side marker's default
///         value already equals <see cref="AlliancePossibleInfo.Cleared" />), so this class extends the same
///         zero-defensive-cost regression check to it.
///     </para>
///     <para>
///         Votes/close-vote (<c>mTribeVoteState</c>/<c>mCloseVoteState</c>, contract Side effects steps 1-2) are
///         deliberately NOT verified here: <see cref="ZoneWar.TribeVoteElection" />.Phase is only a coarse
///         election-PHASE proxy for the vote state machine (not a byte-exact model of the legacy's own per-tribe
///         <c>mTribeVoteState[4]</c> array), and close-vote has no Fenrir-side backing state of any shape at all
///         (see <see cref="WorldInfoBootReset" />'s own remarks on this same gap). There is nothing this class
///         could safely assert about those two groups without inventing state that does not exist -- flagged,
///         not silently skipped; see this task's own openQuestions for the follow-up this implies once a future
///         workstream models them.
///     </para>
/// </summary>
public sealed class WorldInfoBootResetVerifier(
    ZoneCenterSiegeState siege,
    Zone195NokSanState nokSan,
    PopupEventState popup,
    AllianceProposalCenterState allianceProposal,
    ILogger<WorldInfoBootResetVerifier> logger)
{
    /// <summary>
    ///     Runs the verification once, synchronously -- no I/O, matching the legacy reset's own "runs entirely
    ///     before the database connection is established" posture (contract Preconditions). Safe to call at any
    ///     point in Fenrir's boot-preload sequence; see this task's own wiring-manifest notes for exactly where.
    /// </summary>
    public void Verify()
    {
        var violations = 0;

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var dtm = siege.GetZone038DtmValue(tribeId);
            if (dtm != 0)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: Zone038 DTM value for tribe {TribeId} is {Value} at boot, expected 0 (Server/ts25center/S07_MyGame01.cpp:131-134)",
                    tribeId, dtm);
            }

            var stonesHeld = nokSan.GetStonesHeld(tribeId);
            if (stonesHeld != 0)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: Nok-San stones-held count for tribe {TribeId} is {Value} at boot, expected 0 (Server/ts25center/S07_MyGame01.cpp:127-130)",
                    tribeId, stonesHeld);
            }
        }

        for (var slot = 0; slot < Zone195NokSanState.StoneSlotCount; slot++)
        {
            var owner = nokSan.GetOwner(slot);
            if (owner != 0)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: Nok-San stone slot {Slot} owner is {Owner} at boot, expected 0 (uncaptured) (Server/ts25center/S07_MyGame01.cpp:141-144)",
                    slot, owner);
            }
        }

        foreach (var type in Enum.GetValues<PopupEventType>())
        {
            if (popup.IsEnabled(type))
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: popup type {PopupType} is enabled at boot, expected disabled (Server/ts25center/S07_MyGame01.cpp:135-140)",
                    type);
            }
        }

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
        {
            var possibleAlliance = allianceProposal.GetPossibleAllianceInfo(tribeId);
            if (possibleAlliance != AlliancePossibleInfo.Cleared)
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: alliance-possibility cooldown marker for tribe {TribeId} is {Marker} at boot, expected cleared (Server/ts25center/S07_MyGame01.cpp:36-43)",
                    tribeId, possibleAlliance);
            }
        }

        for (var slot = 0; slot < AllianceProposalCenterState.SlotCount; slot++)
        {
            if (!allianceProposal.SlotIsEmpty(slot))
            {
                violations++;
                logger.LogError(
                    "WorldInfo boot reset violation: alliance-state slot {Slot} is not empty at boot (Server/ts25center/S07_MyGame01.cpp:44-47)",
                    slot);
            }
        }

        if (violations == 0)
            logger.LogInformation(
                "WorldInfo boot reset verified: Zone038 DTM, Nok-San stone state, popup-type flags, and alliance-possibility state all confirmed at their legacy compiled-in reset value (zero/disabled/empty) before the first zone tick or accepted connection");
    }
}
