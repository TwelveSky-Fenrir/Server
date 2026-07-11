using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Modern replacement for the legacy ZONE_BROADCAST_FOR_CENTER_SEND hop's remaining ~150-way event-code
///     dispatch (<c>Server/ts25center/S04_MyWork02.cpp:160-1221</c>) -- the sorts <see cref="ZoneEventBroadcaster" />
///     does NOT cover (that class's own remarks call out tSort 38/39/40/42/45/46/47 as already modeled; every
///     other numbered zone-siege state machine sharing the same wire message belongs here).
///     <para>
///         Same architecture collapse <see cref="ZoneEventBroadcaster" /> already documents: the legacy had a
///         real inter-process hop (every ts25zone reaches the one ts25center over
///         ZCP_ZONE_BROADCAST_FOR_CENTER_SEND); Fenrir runs every zone this shard owns in one process against
///         one shared <see cref="ZoneCenterSiegeState" /> instance, so "mutate the shared state" and "tell
///         every zone what changed" collapse into this one <see cref="Ingest" /> call. The wire-level
///         registration gate ("sending connection must already be a registered zone server, else drop it")
///         and the keep-alive timestamp touch the legacy applies to every accepted message
///         (S04_MyWork02.cpp:162-167) are a Network/Dispatch-layer session concern, not a Domain one -- there
///         is no separate zone-server registration handshake left to gate in this collapsed, single-process
///         model, so this class has no precondition to enforce here; whatever future in-process caller invokes
///         <see cref="Ingest" /> is, by construction, already an authenticated in-process actor.
///     </para>
///     <para>
///         Zone241 "Den of Rebirth" (cases 411-415, S04_MyWork02.cpp:962-986) is now BOUND, per the rvr-siege
///         ingestion behavior contract's exact per-code table (411 &#8594; challenge started, 412/413/414 &#8594;
///         ended, 415 &#8594; idle), via <see cref="SiegeEventStateMap.TryMapZone241" /> -- what was previously a
///         recognized-but-unwritten range (this class's old GAP 1). The read-and-discard secondary field + player
///         name codes 411-414 carry are still ignored here exactly as the legacy center ignores them (it never
///         persists them into WorldInfo); only the instance index at the front of the payload is consumed.
///     </para>
///     <para>
///         GAP 2 (CLOSED, workstream C6-zone241-bonus) -- the tribe-wide bonus-ratio sub-selector event
///         has no cited event code at all before this workstream -- it is now dispatched under
///         <c>tSort=301</c> via <see cref="TribeBonusRatioEventMap.EventCode" />/
///         <see cref="TribeBonusRatioEventMap.Apply" />, which decodes the
///         sub-selector and calls <see cref="ZoneCenterSiegeState.SetExperienceBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetItemDropBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetMyoungItemDropBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetKillOtherTribeBonus" /> directly -- see
///         <see cref="TribeBonusRatioEventMap" />'s own remarks for the full citation trail and its own
///         carried-forward open questions (the reset-asymmetry gap and the unnamed tribe indices).
///     </para>
///     <para>
///         GAP 3 -- the two event codes that skip the general relay entirely (a leaderboard-style record
///         update and a proxy-shop-close relay to one specific zone, S04_MyWork02.cpp:1184-1211) are
///         explicitly out of this task's named ranges (they belong to Commerce/leaderboard features) and were
///         given no numeric code either. This class always relays (see <see cref="Ingest" />); those two
///         codes, once assigned, need their own short-circuit added by whichever workflow owns that feature.
///     </para>
///     <para>
///         GAP 4 -- the all-zones ping (case 4000, S04_MyWork02.cpp:1151-1157) legacy hardcodes a distinct
///         event code for its extra broadcast that this cluster's contract did not enumerate; the always-empty
///         payload IS given precisely. <see cref="PingEventCode" /> itself is reused as a placeholder for that
///         second broadcast's Sort until a fresh citation supplies the real constant -- see
///         <see cref="BroadcastAllZonesPing" />.
///     </para>
///     <para>
///         Zone175 (cases 64-100 write, case 110 reset, S04_MyWork02.cpp:613-802), Zone267 (cases within
///         402-410, S04_MyWork02.cpp:929-961) and Zone335 (cases 1501-1507, S04_MyWork02.cpp:1132-1150) are now
///         byte-exact: the per-code STATE VALUE is resolved through <see cref="SiegeEventStateMap" />'s literal
///         legacy tables (the rvr-siege ingestion contract supplies all three in full), NOT the raw event code
///         this class used to store as a placeholder. The mechanical shape (grid/tribe addressing, bounds, the
///         separate 110 reset) is unchanged; only the stored value is now correct -- crucially, the many codes
///         that collapse onto a shared state (15 Zone175 codes &#8594; 23, 1501 &#8594; no-op, 1507 &#8594; 0) now
///         store that shared state rather than distinct raw codes.
///     </para>
///     <para>
///         Zone267/Zone241's individual event codes, plus the pure-relay group (416-428, 500), now have
///         symbolic names and (where the cited source carries one) verbatim legacy labels in
///         <see cref="SiegeZoneLiteralEventCatalog" /> -- a documentation-only naming layer over the numeric
///         dispatch above; it changes no behavior here.
///     </para>
///     <para>
///         Zone051 (6 slots) and Zone053 (10 slots), the two sibling siege-state-VALUE families adjacent to
///         Zone049 (selectors 10-18 and 19-30, S04_MyWork02.cpp:254-335), are now dispatched via
///         <see cref="Zone051Zone053BroadcastResolver" /> against the optional <paramref name="zone051Zone053State" />
///         dependency -- absent (null) in any caller that has not adopted it yet, in which case those selectors
///         simply fall through with zero state effect via each case's own <c>when</c> guard, same idiom as every
///         other optional trailing dependency on this constructor.
///     </para>
///     <para>
///         Alliance-proposal discriminators 46-49 (finalize/break-via-ritual/stone-under-attack/
///         break-via-stone-capture, S04_MyWork02.cpp:408-472) are dispatched via
///         <see cref="AllianceProposalCenterEventMap" /> against the optional <paramref name="allianceState" />
///         dependency -- same "absent means the range's own <c>when</c> guard no-ops" idiom as
///         <paramref name="zone051Zone053State" /> immediately above.
///     </para>
///     <para>
///         Zone049 (siege-zone-slot family, sub-codes 1-9, S04_MyWork02.cpp:212-253) IS byte-exact: the
///         rvr-siege behavior contract gives the full sub-code-to-(state,stampTime) table directly (see
///         <see cref="ApplyZone049" />), unlike Zone175/Zone267's own placeholder values above. Sub-code 1
///         carries no state mutation (its own remaining-seconds field only ever fed the build/runtime-gated
///         Discord announcement, never the primary state machine) -- <see cref="ApplyStateEffect" /> simply has
///         no case for it, matching the legacy's own "falls through with zero state effect" behavior for it.
///     </para>
///     <para>
///         CROSS-SHARD RELAY (rvr-siege): <paramref name="relayQueue" /> is the one piece of this class that is
///         NOT a reproduction of legacy behavior -- Fenrir shards <see cref="ZoneRegistry" /> disjointly by map,
///         so <see cref="BroadcastToEveryZone{TPacket}" /> only ever reaches players on THIS shard's own hosted
///         maps, unlike the legacy's single ts25center process relaying to literally every connected zone
///         process cluster-wide. <see cref="Ingest" /> enqueues Zone049 sub-codes (1-9) onto
///         <see cref="IRvrSiegeEventRelayQueue" /> (SQL-mediated fan-out, same precedent as
///         <c>GuildTribeBroadcastRelayHost</c>/<c>ProxyShopExpirationRelayHost</c>) so every OTHER live shard's
///         own <c>RvrSiegeEventRelayHost</c> poll cycle replays the identical (sub-code, payload) pair through
///         <see cref="ApplyRelayedEvent" /> -- otherwise a Zone049 slot mutated on one shard would never become
///         visible to players on any other shard at all, since <see cref="ZoneCenterSiegeState" /> is itself a
///         per-process singleton with no other cross-shard convergence path (unlike
///         <see cref="WorldState.WorldStateService" />'s own DB-backed reconcile). Optional purely so every
///         existing hand-written test call site (which doesn't need cross-shard relay) keeps compiling; a
///         production instance always has it, since <see cref="IRvrSiegeEventRelayQueue" /> is registered
///         alongside this class.
///     </para>
/// </summary>
public sealed class ZoneCenterBroadcastIngestor(
    ZoneCenterSiegeState state,
    ZoneRegistry zones,
    ILogger<ZoneCenterBroadcastIngestor> logger,
    IRvrSiegeEventRelayQueue? relayQueue = null,
    IOptions<GameServerOptions>? gameOptions = null,
    Zone051Zone053SiegeState? zone051Zone053State = null,
    AllianceProposalCenterState? allianceState = null)
{
    /// <summary>
    ///     Fixed opaque payload size (<c>Server/Header/Protocol/ZONE.h:19-24</c>), matching
    ///     <see cref="ZoneEventInfoResponse.Data" />.
    /// </summary>
    public const int PayloadSize = 130;

    /// <summary>Zone049 siege-zone-slot family -- sub-code 1 has no state effect, see class remarks.</summary>
    public const int Zone049RangeStart = 1;

    public const int Zone049RangeEnd = 9;

    public const int Zone175RangeStart = 64;
    public const int Zone175RangeEnd = 100;
    public const int Zone175ResetEventCode = 110;

    /// <summary>
    ///     Citation gives this sub-range as "cases 402-410" (9 integers) while the prose separately counts
    ///     "7 event codes" for the same family -- like the Valley-of-Deceased family elsewhere in this same
    ///     switch, not every integer in a cited range necessarily has a live case label. This class treats the
    ///     whole 402-410 span as reachable (a conservative superset of the real 7) rather than guess which 2
    ///     of the 9 are dead.
    /// </summary>
    public const int Zone267RangeStart = 402;

    public const int Zone267RangeEnd = 410;

    /// <summary>Den of Rebirth -- exact code set, no write performed; see class remarks, GAP 1.</summary>
    public const int Zone241RangeStart = 411;

    public const int Zone241RangeEnd = 415;

    /// <summary>
    ///     FFA single-scalar family: 1501 is a no-op (pre-start countdown), 1502-1506 &#8594; states 1-5, and
    ///     1507 &#8594; 0 (the reset -- there is no separate 1520 reset code, contrary to an earlier placeholder
    ///     guess). See <see cref="SiegeEventStateMap.TryMapZone335" />.
    /// </summary>
    public const int Zone335RangeStart = 1501;

    public const int Zone335RangeEnd = 1507;

    /// <summary>Per-tribe DTM effect value -- fully precise (S04_MyWork02.cpp:1160-1164).</summary>
    public const int DtmEventCode = 1510;

    /// <summary>All-zones ping, double-broadcast -- see class remarks, GAP 4.</summary>
    public const int PingEventCode = 4000;

    /// <summary>
    ///     The full ingestion from THIS shard's own originating caller: (1) apply whatever shared-state write
    ///     this event code carries, if any (a data-driven range table, not one branch per individual code);
    ///     (2) unconditionally relay the same event code and raw payload to every zone this shard owns, with
    ///     the case-4000 ping additionally firing its own extra broadcast first; (3) for the Zone049 family
    ///     (sub-codes 1-9) only, enqueue the identical pair onto the cross-shard relay so every OTHER live
    ///     shard's own poll cycle reaches the same state via <see cref="ApplyRelayedEvent" /> -- see class
    ///     remarks, CROSS-SHARD RELAY. Never throws for an unrecognized or malformed-content event code --
    ///     matches the legacy's own no-acknowledgement, no-failure-indicator posture; the only rejection this
    ///     method performs is a payload of the wrong length, an API misuse guard that has no legacy-parity
    ///     meaning of its own.
    /// </summary>
    public void Ingest(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        ApplyStateEffect(eventCode, data);

        if (eventCode == PingEventCode)
            BroadcastAllZonesPing();

        Relay(eventCode, data);

        if (eventCode is >= Zone049RangeStart and <= Zone049RangeEnd)
            EnqueueForOtherShards(eventCode, data);
    }

    /// <summary>
    ///     The replay entry point a sibling shard's own <c>RvrSiegeEventRelayHost</c> calls for a row this
    ///     shard did NOT originate: applies the identical shared-state write <see cref="Ingest" /> would have
    ///     applied, then relays to this shard's own hosted zones -- but does NOT re-enqueue onto the cross-shard
    ///     relay (that would echo the row back out to every other shard forever) and does NOT fire the
    ///     case-4000 ping's extra broadcast (only Zone049 sub-codes are ever relayed today, see
    ///     <see cref="Ingest" />, so that branch is unreachable from here regardless).
    /// </summary>
    public void ApplyRelayedEvent(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        ApplyStateEffect(eventCode, data);
        Relay(eventCode, data);
    }

    private void EnqueueForOtherShards(int eventCode, ReadOnlySpan<byte> data)
    {
        if (relayQueue is null)
            return;

        var shardId = gameOptions?.Value.ShardId ?? 0;
        relayQueue.Enqueue(new RvrSiegeEventRelayEntry(shardId, eventCode, data.ToArray()));
    }

    private void ApplyStateEffect(int eventCode, ReadOnlySpan<byte> data)
    {
        switch (eventCode)
        {
            case >= Zone049RangeStart and <= Zone049RangeEnd:
                ApplyZone049(eventCode, data);
                break;

            case Zone175ResetEventCode:
                ApplyZone175(eventCode, data, true);
                break;

            case >= Zone175RangeStart and <= Zone175RangeEnd:
                ApplyZone175(eventCode, data, false);
                break;

            case >= Zone267RangeStart and <= Zone267RangeEnd:
                ApplyZone267(eventCode, data);
                break;

            case >= Zone241RangeStart and <= Zone241RangeEnd:
                ApplyZone241(eventCode, data);
                break;

            case >= Zone051Zone053BroadcastResolver.Zone051RangeStart and <= Zone051Zone053BroadcastResolver.Zone051RangeEnd
                when zone051Zone053State is not null:
                Zone051Zone053BroadcastResolver.ApplyZone051(zone051Zone053State, eventCode, data, logger);
                break;

            case >= Zone051Zone053BroadcastResolver.Zone053RangeStart and <= Zone051Zone053BroadcastResolver.Zone053RangeEnd
                when zone051Zone053State is not null:
                Zone051Zone053BroadcastResolver.ApplyZone053(zone051Zone053State, eventCode, data, logger);
                break;

            case DtmEventCode:
                ApplyDtm(data);
                break;

            case >= Zone335RangeStart and <= Zone335RangeEnd:
                if (SiegeEventStateMap.TryMapZone335(eventCode, out var ffaState))
                    state.SetZone335(ffaState);
                break;

            case TribeBonusRatioEventMap.EventCode:
                TribeBonusRatioEventMap.Apply(state, data, logger);
                break;

            case >= AllianceProposalCenterEventMap.EventCodeRangeStart
                and <= AllianceProposalCenterEventMap.EventCodeRangeEnd
                when allianceState is not null:
                AllianceProposalCenterEventMap.Apply(eventCode, data, allianceState, logger);
                break;

            case PingEventCode:
                // A12: case 4000's own zone-side reaction, no server-number restriction -- reset every ready
                // character's HSB-window reward-eligibility flag. See HsbRewardFlagResetReactor's remarks.
                HsbRewardFlagResetReactor.Apply(zones);
                break;

            // Everything else (416-428 minus the unrecovered 417 gap, and 500 -- all individually named in
            // SiegeZoneLiteralEventCatalog.PureRelayEventCodes/KnownGapEventCodes; 601-628, 659-675, 751-774,
            // 1501 no-op, 1511-1514, and any genuinely unrecognized code) is either a legacy empty/
            // commented-out body or truly unknown -- both fall through with zero state effect, which is
            // exactly the legacy's own observable behavior for those ranges.
        }
    }

    /// <summary>
    ///     Sub-codes 1-9, S04_MyWork02.cpp:212-253: sub-code 1 mutates nothing (its remaining-seconds field
    ///     only fed the Discord/diagnostic announcement, never the primary state machine, and is not
    ///     independently reproduced here -- see class remarks). Sub-codes 2-9 read the zone slot index from the
    ///     first 4 payload bytes and write a fixed (state, stampTime) pair to it, exactly the legacy table:
    ///     2-&gt;(1,false), 3-&gt;(2,false), 4-&gt;(3,false), 5-&gt;(5,true), 6-&gt;(4,true), 7-&gt;(4,true),
    ///     8-&gt;(5,true), 9-&gt;(0,false). 5/8 and 6/7 are deliberately identical -- the source contract
    ///     confirms no distinguishing behavior exists between each pair.
    /// </summary>
    private void ApplyZone049(int eventCode, ReadOnlySpan<byte> data)
    {
        if (eventCode == Zone049RangeStart)
            return;

        var slot = ReadInt32(data, 0);
        if (!ZoneCenterSiegeState.IsValidZone049Slot(slot))
        {
            logger.LogWarning("Zone049 sub-code {EventCode} referenced out-of-range slot {Slot} -- ignored",
                eventCode, slot);
            return;
        }

        var (value, stampTime) = eventCode switch
        {
            2 => (1, false),
            3 => (2, false),
            4 => (3, false),
            5 => (5, true),
            6 => (4, true),
            7 => (4, true),
            8 => (5, true),
            9 => (0, false),
            _ => (0, false)
        };

        state.SetZone049State(slot, value, stampTime);
    }

    private void ApplyZone175(int eventCode, ReadOnlySpan<byte> data, bool isReset)
    {
        var instance = ReadInt32(data, 0);
        var slot = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidZone175Cell(instance, slot))
        {
            logger.LogWarning(
                "Zone175 event {EventCode} referenced out-of-range instance {Instance}/slot {Slot} -- ignored",
                eventCode, instance, slot);
            return;
        }

        if (isReset)
            state.ResetZone175(instance, slot);
        else if (SiegeEventStateMap.TryMapZone175(eventCode, out var mapped))
            state.SetZone175(instance, slot, mapped);
    }

    private void ApplyZone267(int eventCode, ReadOnlySpan<byte> data)
    {
        var tribeIndex = ReadInt32(data, 0);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeIndex))
        {
            logger.LogWarning("Zone267 event {EventCode} referenced out-of-range tribe index {TribeIndex} -- ignored",
                eventCode, tribeIndex);
            return;
        }

        // Code 402 is a no-op (TryMap returns false); 403-410 map to the byte-exact legacy states, including
        // 410's reset-to-0.
        if (SiegeEventStateMap.TryMapZone267(eventCode, out var mapped))
            state.SetZone267((byte)tribeIndex, mapped);
    }

    /// <summary>
    ///     Den of Rebirth (cases 411-415, S04_MyWork02.cpp:962-986): reads the instance index from the front of
    ///     the payload, ignores the read-and-discard secondary/name fields codes 411-414 carry (exactly as the
    ///     legacy center ignores them), and writes the mapped <see cref="DenOfRebirthChallengeState" /> via
    ///     <see cref="SiegeEventStateMap.TryMapZone241" />. Bounds-checks the instance even though the legacy
    ///     center does not (see class remarks / the security note reported by this cluster).
    /// </summary>
    private void ApplyZone241(int eventCode, ReadOnlySpan<byte> data)
    {
        var instance = ReadInt32(data, 0);

        if (!ZoneCenterSiegeState.IsValidZone241Instance(instance))
        {
            logger.LogWarning("Zone241 event {EventCode} referenced out-of-range instance {Instance} -- ignored",
                eventCode, instance);
            return;
        }

        if (SiegeEventStateMap.TryMapZone241(eventCode, out var challengeState))
            state.SetZone241(instance, challengeState);
    }

    private void ApplyDtm(ReadOnlySpan<byte> data)
    {
        var tribeId = ReadInt32(data, 0);
        var effectValue = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeId))
        {
            logger.LogWarning("DTM event referenced out-of-range tribe id {TribeId} -- ignored", tribeId);
            return;
        }

        state.SetZone038DtmValue((byte)tribeId, effectValue);
    }

    /// <summary>
    ///     The one exception to the "relay whatever was received" shape: a second, independent broadcast
    ///     under a hardcoded, always-empty payload, fired before the caller's own code 4000 + original payload
    ///     falls through to the normal <see cref="Relay" /> in <see cref="Ingest" />. See class remarks, GAP 4,
    ///     for why this reuses <see cref="PingEventCode" /> as this second broadcast's own Sort too.
    /// </summary>
    private void BroadcastAllZonesPing()
    {
        var empty = new byte[PayloadSize];
        // A12 resolves GAP 4: the real, distinct sort for this second broadcast is 1601 -- PingEventCode
        // (4000) is only the TRIGGER code this Ingest(4000, ...) call itself carries, never this broadcast's
        // own Sort.
        var response = new ZoneEventInfoResponse
            { Sort = ScheduledZoneCenterEventCodes.HsbRewardFlagResetPingEventCode, Data = empty };

        BroadcastToEveryZone(in response);
    }

    private void Relay(int eventCode, ReadOnlySpan<byte> data)
    {
        var response = new ZoneEventInfoResponse { Sort = eventCode, Data = data.ToArray() };

        BroadcastToEveryZone(in response);
    }

    /// <summary>
    ///     Serialize-once, cluster-wide fan-out -- same rent-once/write-once/copy-N-times idiom every other
    ///     Zone broadcast helper already uses (e.g. <c>Zone.PlayerLifecycle.BroadcastAvatarAction</c>,
    ///     <see cref="ZoneEventBroadcaster" />'s own twin of this method), with a per-recipient failure
    ///     isolated (logged, skipped) instead of left to abort the rest of the fan-out. Without this, a single
    ///     recipient whose transport pipe writer has already completed (an ordinary disconnect race: the
    ///     player is still present in <see cref="Zone.Players" /> until that player's own zone next drains its
    ///     Leave command) throws out of <see cref="ClientSession.Send{TPacket}" /> uncaught by a bare loop --
    ///     silently cutting off delivery to every zone/player still left to visit in the SAME call, for both
    ///     <see cref="BroadcastAllZonesPing" /> and <see cref="Relay" /> (every event code <see cref="Ingest" />
    ///     accepts funnels through the latter).
    /// </summary>
    private void BroadcastToEveryZone<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            foreach (var player in zone.Players)
                try
                {
                    if (player.Session is ClientSession clientSession)
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Zone-center relay broadcast to character {RecipientId} (zone {MapId}) failed",
                        player.CharacterId, zone.MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
