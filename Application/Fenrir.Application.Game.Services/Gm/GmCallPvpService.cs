using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmCallPvpService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1770-1823 (full case 599 body: the uUserSort &gt;= 1 gate, the duel-slot
///     validation, the immediate acknowledgment send that precedes the relocation work, the fixed coordinate
///     pair, the full connected-character iteration and its three filter conditions, the per-match position
///     overwrite/notification/broadcast, and the exit that bypasses the dispatcher's shared closing steps) ;
///     Server/ts25zone/S04_MyWork04.cpp:1785-1790 (the two literal coordinate-triple assignments for duel slot
///     1 and slot 2, -232/36/2 and 232/36/2 respectively) ; Server/ts25zone/S04_MyWork04.cpp:1801-1819
///     (tDuleNo-to-slot selection: 1 routes to the first triple, 2 to the second) ;
///     Server/ts25zone/S04_MyWork04.cpp:1825-1844 (the duel-ready command immediately following, case 600:
///     cited both to confirm ITS OWN explicit same-server-number guard that this command's body conspicuously
///     lacks, and because its own :1839-1844 independently re-declares the identical pair of coordinate
///     literals -- cross-confirmation of the same two values, not a second, different coordinate set) ;
///     Server/ts25zone/H07_MyGame.h:892 (the per-session cooldown-tracking field this command resets on every
///     relocated character) ; Server/ts25zone/S07_MyGame03.cpp:9388-9397 (that field's only other read/write
///     site: an unidentified skill/buff-cast branch's ten-second reuse cooldown -- confirmed a second time, by
///     an exhaustive repo-wide search for any SKILL_INFO name field/aSkillName/szSkillName field/skill-data
///     file, to be permanently unrecoverable from Server/ alone; skill names live in a client-side resource
///     table outside this repository) ; Server/Header/Protocol/STRUCT.h:1285-1294 (GM_SETPVPPOINT and
///     GM_CALLPVP struct shapes -- confirms tDuleNo is the field selecting between the two slots).
/// </summary>
/// <remarks>
///     <b>
///         One magnitude recovered, one confirmed permanently unrecoverable -- per the "gm-call-pvp-magnitudes"
///         legacy-research re-verification pass's own explicit framing:
///     </b>
///     <list type="bullet">
///         <item>
///             <see cref="DuelSlot1Coordinate" />/<see cref="DuelSlot2Coordinate" /> are now the REAL legacy
///             values -- (-232, 36, 2) and (232, 36, 2) respectively -- byte-for-byte transcribed from
///             Server/ts25zone/S04_MyWork04.cpp:1785-1790 (case 599) and independently cross-confirmed by the
///             sibling case 600 re-declaration at :1839-1844 (the two host commands do not share a single named
///             constant; each independently re-declares the identical pair of literal triples, treated as
///             cross-confirmation, not evidence of two different coordinate sets). This command is safe to wire
///             into <c>GenericActionHandler</c>'s live dispatch table -- the earlier "documented INERT
///             placeholder, do not enable" caveat no longer applies.
///         </item>
///         <item>
///             The H07_MyGame.h:892 per-character cooldown-tracking field reset is still NOT implemented here:
///             no <see cref="PlayerRuntimeState" /> field models it, and the specific skill/buff it incidentally
///             gates could not be identified even after a second, exhaustive re-search under Server/ (no
///             SKILL_INFO name field, no aSkillName/szSkillName field, and no skill-data file of any kind exists
///             anywhere in this repository -- skill names live in a client-side resource table this repository
///             does not contain; ServerDocs/12_ts25zone/11_MyGame03_PartieA.md §5.9 likewise never resolves
///             skill numbers to names). This is now a permanently-closed gap, not an open question pending a
///             future re-check -- implementing a guessed field or a guessed skill identity would violate this
///             project's "never invent an id/magnitude" rule more than simply omitting the effect.
///         </item>
///     </list>
///     <b>"Already engaged in another duel-related state" filter:</b> mapped onto this project's own
///     <see cref="DuelRegistry" /> (<see cref="DuelRegistry.IsNegotiating" />/
///     <see cref="DuelRegistry.TryGetActiveDuel" />) as the closest available Fenrir concept -- this is NOT a
///     citation-confirmed mapping to legacy's own per-character duel-state field (<c>aDuelState[]</c> /
///     <c>mDuelProcessState</c>, see <c>DuelRegistry.ForceClearOnZoneEntry</c>'s own citation), only the most
///     plausible Fenrir-side analogue available today. Flag for <c>cpp-zone-gameplay-analyst</c> re-check if a
///     future change needs that mapping citation-confirmed rather than inferred.
///     <para>
///         <b>Readiness filter:</b> "not mid-connection"/"not currently transferring between zones/maps" is
///         satisfied structurally by iterating <see cref="Zone.Players" /> across every hosted zone --
///         <see cref="ZoneRegistry.TryGetPlayer" />'s own remarks confirm a character mid cross-zone handoff is
///         absent from every zone's live player set until the transfer completes, so no separate readiness flag
///         needs to be threaded through here.
///     </para>
///     <para>
///         <b>No self-exclusion:</b> unlike this dispatch table's other by-name GM commands (FIND/CALL/MOVE/
///         NCHAT/YCHAT/KICK, which all route through the shared <c>SearchAvatar</c>-equivalent helper that
///         excludes the caller's own index by default -- Server/ts25zone/H07_MyGame.h:592-602), this command's
///         own citation directly iterates every connected character with no such exclusion, and the source
///         contract does not describe one. A GM whose own name exactly matches the requested target name is
///         therefore itself a legal match, if its own duel-state/readiness conditions pass.
///     </para>
/// </remarks>
public sealed class GmCallPvpService(
    ZoneRegistry zones,
    IEventLogRepository eventLog,
    ILogger<GmCallPvpService> logger,
    DuelRegistry? duelRegistry = null) : IGmCallPvpService
{
    private const int Sort = 599;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1; // legacy tResult's own default-initialized/rejected value

    // Server/ts25zone/S04_MyWork04.cpp:1785-1790 (case 599) / :1839-1844 (case 600, identical re-declaration,
    // cross-confirming these are the real recovered values, not a placeholder). Slot 2 mirrors slot 1 by
    // negating only the first component; components 1 and 2 (36, 2) are shared between both slots.
    private static readonly (float X, float Y, float Z) DuelSlot1Coordinate = (-232f, 36f, 2f);
    private static readonly (float X, float Y, float Z) DuelSlot2Coordinate = (232f, 36f, 2f);

    public async ValueTask HandleAsync(GmCallPvpPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM-CALLPVP command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.DuelSlot is not (1 or 2))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Character {CharacterId} GM-CALLPVP rejected: invalid duel slot {DuelSlot}",
                    zoneSession.CharacterId, packet.DuelSlot);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = Sort, Data = data, RuneValue = 0 });
            return;
        }

        var destination = packet.DuelSlot == 1 ? DuelSlot1Coordinate : DuelSlot2Coordinate;

        // Sent BEFORE the relocation loop below runs -- see this type's own remarks / IGmCallPvpService's own
        // remarks for the explicit ordering requirement: the caller has no way to distinguish "found and moved"
        // from "matched nobody" from this response alone.
        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });

        var callerCharacterId = zoneSession.CharacterId!.Value;
        var callerAccountId = zoneSession.AccountId;

        foreach (var zone in zones.Zones)
        foreach (var candidate in zone.Players)
        {
            // Exact, case-sensitive, full-string equality -- deliberately NOT OrdinalIgnoreCase (see
            // GmCallPvpPayload's own remarks).
            if (!string.Equals(candidate.Name, packet.TargetName, StringComparison.Ordinal))
                continue;

            // "Already engaged in another duel-related state" -- see this type's own remarks for the
            // DuelRegistry-based mapping and its own citation caveat. A dueling/negotiating candidate is
            // skipped entirely and left untouched, matching the contract's "skipped, not a near-miss" rule.
            if (duelRegistry is not null &&
                (duelRegistry.IsNegotiating(candidate.CharacterId) ||
                 duelRegistry.TryGetActiveDuel(candidate.CharacterId, out _)))
            {
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug(
                        "GM-CALLPVP: candidate character {CandidateCharacterId} ({CandidateName}) skipped -- already engaged in a duel-related state",
                        candidate.CharacterId, candidate.Name);
                continue;
            }

            if (!await zone.PostTribeProgressCommandAndWaitAsync(
                    new TribeProgressZoneCommand(candidate.CharacterId, TeleportTo: destination,
                        NeighborActionBroadcast: true), cancellationToken))
                logger.LogError(
                    "Zone {MapId} tribe-progress inbox full: dropped GM-CALLPVP relocation mirror for character {CharacterId}",
                    zone.MapId, candidate.CharacterId);

            // The H07_MyGame.h:892 cooldown-tracking field reset is deliberately NOT implemented -- see this
            // type's own remarks for why (unidentified field/skill, confirmed permanently unrecoverable after
            // a second exhaustive re-search, never invented).

            await eventLog.LogAsync(GmDuelAndInventoryActionEventCodes.CallPvpRelocate, EventLogCategory.GmAction,
                callerAccountId, callerCharacterId, ((ZoneClientSession)candidate.Session).AccountId,
                candidate.CharacterId, null, null, null, null, null, 1,
                $"DuelSlot={packet.DuelSlot};TargetName={candidate.Name}", cancellationToken);

            logger.LogInformation(
                "Character {CharacterId} applied the Basic-tier GM-CALLPVP command against character {TargetCharacterId} ({TargetName}), duel slot {DuelSlot}",
                callerCharacterId, candidate.CharacterId, candidate.Name, packet.DuelSlot);
        }
    }
}
