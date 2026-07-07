using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

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
///         GAP 1 -- Zone241 "Den of Rebirth" (cases 411-415, S04_MyWork02.cpp:962-986) is a real, reachable
///         range in the legacy dispatch, not a dead one, but this cluster's own translated contract could only
///         establish the CODE SET (411-415, exactly 5) and the output DOMAIN (3 coarse states: idle/
///         challenge-started/ended-for-any-reason), not which of the 5 codes produces which of the 3 outputs,
///         nor which 2 of the 5 carry the extra (read-and-discard) secondary field + player name. Binding a
///         specific code to a specific output here would be inventing a legacy fact this session could not
///         confirm, so no write is performed for this range -- <see cref="ZoneCenterSiegeState.SetZone241" />/
///         <see cref="ZoneCenterSiegeState.GetZone241" />/<see cref="ZoneCenterSiegeState.ResetZone241" /> are
///         ready for a future contract that supplies the exact per-code binding.
///     </para>
///     <para>
///         GAP 2 -- the tribe-wide bonus-ratio sub-selector event (S04_MyWork02.cpp:854-919) has no cited
///         event code at all ("a separate event code, outside the numbered ranges named in this task"), and
///         its payload's own sub-selector encoding (which raw value picks experience/item-drop/Myoung-drop/
///         kill-other-tribe) is likewise not given. <see cref="ZoneCenterSiegeState.SetExperienceBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetItemDropBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetMyoungItemDropBonusRatio" />/
///         <see cref="ZoneCenterSiegeState.SetKillOtherTribeBonus" /> exist and are fully, precisely
///         implemented per field (including the kill-other-tribe variant's zero-the-other-three-tribes
///         write), but nothing in this dispatch table calls them yet -- wiring them in needs both numbers from
///         a fresh legacy-behavior-translator pass.
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
///         Zone175 (cases 64-100 write, case 110 reset, S04_MyWork02.cpp:613-802) and Zone267 (cases within
///         402-410, S04_MyWork02.cpp:929-961) ARE wired below, but only their MECHANICAL shape is legacy-exact
///         (grid/tribe addressing, bounds, the separate reset code): the actual per-code STATE VALUE this
///         class writes is the raw event code itself, a deterministic, distinguishable placeholder -- NOT the
///         literal legacy constant, which this cluster's contract only gives in aggregate ("roughly 15 of the
///         37 Zone175 codes collapse onto one shared value, ~22 produce distinct ones"; "one of the 7 Zone267
///         codes resets to idle" without saying which). Upgrading to byte-exact constants needs a fresh
///         legacy-behavior-translator pass over the individual case bodies.
///     </para>
/// </summary>
public sealed class ZoneCenterBroadcastIngestor(
    ZoneCenterSiegeState state,
    ZoneRegistry zones,
    ILogger<ZoneCenterBroadcastIngestor> logger)
{
    /// <summary>
    ///     Fixed opaque payload size (<c>Server/Header/Protocol/ZONE.h:19-24</c>), matching
    ///     <see cref="ZoneEventInfoResponse.Data" />.
    /// </summary>
    public const int PayloadSize = 130;

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

    /// <summary>Same "cited range wider than the prose's live count" caveat as <see cref="Zone267RangeStart" />.</summary>
    public const int Zone335RangeStart = 1501;

    public const int Zone335RangeEnd = 1507;
    public const int Zone335ResetEventCode = 1520;

    /// <summary>Per-tribe DTM effect value -- fully precise (S04_MyWork02.cpp:1160-1164).</summary>
    public const int DtmEventCode = 1510;

    /// <summary>All-zones ping, double-broadcast -- see class remarks, GAP 4.</summary>
    public const int PingEventCode = 4000;

    /// <summary>
    ///     The full two-step ingestion: (1) apply whatever shared-state write this event code carries, if
    ///     any (a data-driven range table, not one branch per individual code); (2) unconditionally relay the
    ///     same event code and raw payload to every zone this shard owns, with the case-4000 ping additionally
    ///     firing its own extra broadcast first. Never throws for an unrecognized or malformed-content event
    ///     code -- matches the legacy's own no-acknowledgement, no-failure-indicator posture; the only
    ///     rejection this method performs is a payload of the wrong length, an API misuse guard that has no
    ///     legacy-parity meaning of its own.
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
    }

    private void ApplyStateEffect(int eventCode, ReadOnlySpan<byte> data)
    {
        switch (eventCode)
        {
            case Zone175ResetEventCode:
                ApplyZone175(eventCode, data, true);
                break;

            case >= Zone175RangeStart and <= Zone175RangeEnd:
                ApplyZone175(eventCode, data, false);
                break;

            case >= Zone267RangeStart and <= Zone267RangeEnd:
                ApplyZone267(eventCode, data);
                break;

            case DtmEventCode:
                ApplyDtm(data);
                break;

            case Zone335ResetEventCode:
                state.ResetZone335();
                break;

            case >= Zone335RangeStart and <= Zone335RangeEnd:
                state.SetZone335(eventCode);
                break;

            // Zone241 (411-415): recognized/reachable, no write -- see class remarks, GAP 1.
            // Everything else (416-428, 500, 601-628, 659-675, 751-774, 1511-1514, and any genuinely
            // unrecognized code) is either a legacy empty/commented-out body or truly unknown -- both fall
            // through with zero state effect, which is exactly the legacy's own observable behavior for
            // those ranges.
        }
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
        else
            state.SetZone175(instance, slot, eventCode);
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

        state.SetZone267((byte)tribeIndex, eventCode);
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
        var response = new ZoneEventInfoResponse { Sort = PingEventCode, Data = empty };

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
