using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmMaxStatService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1188-1202 (full case 509 body: privilege gate, the four-stat and
///     skill-point assignment, forced logout on both the gate-fail and success paths, and the early return
///     that bypasses the shared response epilogue entirely -- so no response packet is ever composed for this
///     selector) ; Server/ts25zone/S03_MyUser.cpp:338-410 (the logout routine this command's disconnect call
///     resolves to: party-break broadcast, logout-info update, cross-process session-directory unregister
///     call, socket close) ; Server/Header/Protocol/DEFINE.h:651 (confirms the disconnect call used
///     throughout this case body resolves directly to that full-logout routine, not a lighter-weight
///     socket-only close).
/// </summary>
/// <remarks>
///     Fenrir has no single "Quit()"-equivalent synchronous full-logout call the way legacy does; the
///     connection-teardown path this project's own architecture already runs on any <see cref="ClientSession.Abort" />
///     (<c>Zone.HandleLeave</c>'s party-break/trade-clear/cross-shard-directory cleanup, driven from the
///     connection host's own disconnect-triggered teardown) is the closest architectural analogue, so
///     <see cref="DisconnectReason.GmCommandLogout" /> is used here rather than inventing a bespoke synchronous
///     logout call that would duplicate that existing teardown path.
///     <para>
///         The mutated stat/skill-point values are never written to SQL directly from this method: per the
///         source contract, "the mutated avatar record only becomes durable through the ordinary logout/save
///         pathway" -- the closest Fenrir equivalent of that is the existing write-behind
///         <see cref="Fenrir.Data.WriteBehind.DirtyTracker{TKey}" />/progress-flush pathway every other hot
///         mutable-stat field in <see cref="PlayerRuntimeState" /> already uses (see
///         <see cref="Zone.PostTribeProgressCommandAndWaitAsync" />'s own callers, e.g.
///         <c>GenericActionService.AllocateStatPointAsync</c>), not a bespoke synchronous DB write invented
///         for this command specifically.
///     </para>
///     <para>
///         "Unspent skill-point counter" is mapped to <see cref="PlayerRuntimeState.SkillPoints" /> (the
///         learn-skill/upgrade-skill currency), not <see cref="PlayerRuntimeState.StatPoints" /> (the
///         separate VIT/STR/INT/DEX-allocation currency already set by tSort 206/<c>AllocateStatPointAsync</c>)
///         -- the source contract's own wording distinguishes "four core ability stats" from "the unspent
///         skill-point counter" as two separate concepts, and only the latter is set here, matching Fenrir's
///         own established name for that specific counter.
///     </para>
///     <para>
///         Legacy's own case 509 body writes no game.EventLog row at all (see this type's own citations) --
///         the <see cref="IEventLogRepository.LogAsync" /> call below is a Fenrir-authored addition (project
///         decision: every Admin-tier GM command must leave a durable audit trail, regardless of whether the
///         cited legacy body happens to audit it), not a ported behavior. See
///         <see cref="GmActionEventCodes.MaxStatCheat" /> for the locally-scoped EventCode this uses.
///     </para>
/// </remarks>
public sealed class GmMaxStatService(IEventLogRepository eventLog, ILogger<GmMaxStatService> logger)
    : IGmMaxStatService
{
    /// <summary>No domain-specific outcome code exists for this always-succeeds-once-past-the-gate command; 1 = applied.</summary>
    private const byte AppliedOutcome = 1;

    /// <summary>Fixed target for VIT/STR/INT/DEX (Server/ts25zone/S04_MyWork04.cpp:1188-1202).</summary>
    private const int MaxStatValue = 10000;

    /// <summary>Fixed target for the unspent skill-point counter (Server/ts25zone/S04_MyWork04.cpp:1188-1202).</summary>
    private const int MaxSkillPoints = 3000;

    public async ValueTask HandleAsync(ZoneClientSession zoneSession, PlayerRuntimeState state, Zone zone,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Admin))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Admin-tier MAX stat-cheat without sufficient privilege -- forcing logout, no reply",
                zoneSession.CharacterId);
            zoneSession.Abort(DisconnectReason.GmCommandLogout);
            return;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, StatVit: MaxStatValue, StatStr: MaxStatValue,
                    StatInt: MaxStatValue, StatDex: MaxStatValue, SkillPoints: MaxSkillPoints), cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped MAX stat-cheat mirror for character {CharacterId} -- forcing logout regardless, matching legacy's own unconditional disconnect",
                zone.MapId, state.CharacterId);

        // Project-decision audit trail (this type's own remarks) -- immediately after applying the effect,
        // before the forced logout. No admin.ErrorCatalog/DB migration needed: game.EventLog.EventCode is an
        // app-owned numbering scheme with no FK'd catalog table (game.EventLog.sql's own comment).
        await eventLog.LogAsync(GmActionEventCodes.MaxStatCheat, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, AppliedOutcome,
            $"VIT=STR=INT=DEX={MaxStatValue};SkillPoints={MaxSkillPoints}", cancellationToken);

        logger.LogWarning(
            "Character {CharacterId} applied the Admin-tier MAX stat-cheat (VIT/STR/INT/DEX -> {MaxStatValue}, SkillPoints -> {MaxSkillPoints}) -- forcing logout, no reply",
            state.CharacterId, MaxStatValue, MaxSkillPoints);
        zoneSession.Abort(DisconnectReason.GmCommandLogout);
    }
}
