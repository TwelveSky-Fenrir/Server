using System.Buffers.Binary;
using Fenrir.Cluster.Wire.Packets;
using Fenrir.Cluster.WorldState;
using Fenrir.Data.Abstractions.Game;
using Microsoft.Extensions.Logging;
using IWorldEventBroadcaster = Fenrir.Cluster.WorldState.ICenterLinkBroadcaster;

namespace Fenrir.Cluster.EventBus;

/// <summary>
///     The hardened CenterServer ingestor for the op33 world-event bus (legacy
///     <c>W_ZONE_BROADCAST_FOR_CENTER_SEND</c>, <c>Server/ts25center/S04_MyWork02.cpp:160-1268</c>). For each inbound
///     op33 it: (1) handles the two non-fan-out exceptions <c>9998</c> (hero-rank -&gt; authority) and <c>10000</c>
///     (close-proxy -&gt; single-zone unicast); (2) gates every other <c>tSort</c> against
///     <see cref="KnownTSortRegistry" /> — an unknown <c>tSort</c> is DROPped + AntiCheat-audited, never rebroadcast
///     (the deliberate hardening of legacy's default-less, blind-rebroadcast switch); (3) routes the recognized
///     <c>tSort</c> to the effect-of-state method on <see cref="IWorldStateAuthority" /> (which owns the aggregate
///     storage, index-bounds validation, and real-time timestamping); (4) fans the event out.
///     <para>
///         Invariant preserved from legacy: effect-of-state FIRST, then fan-out. An index the authority reports as
///         out of bounds (a <see langword="false" /> return) is DROPped + audited, never fanned out (legacy wrote it
///         out of bounds). Case <c>4000</c> emits the zeroed <c>1601</c> FFA-refresh trigger before its own fan-out.
///         All timestamping is real wall-clock (delegated to the authority), never a tick counter. This unit is
///         DORMANT this lot: it runs on the Center side while shards still author world state (the shard-&gt;center
///         authority switch is Lot 5).
///     </para>
/// </summary>
public sealed class CenterWorldEventIngestor(
    IWorldStateAuthority authority,
    IHeroRankAuthority heroRank,
    IWorldEventBroadcaster broadcaster,
    ICenterCloseProxyRelay closeProxy,
    IEventLogQueue auditLog,
    ILogger<CenterWorldEventIngestor> logger)
{
    private const int HeroRankPrePointSort = 9998;
    private const int CloseProxySort = 10000;
    private const int TribeCount = 4;

    private const short DroppedUnknownTSortEventCode = 1;
    private const short DroppedOutOfBoundsIndexEventCode = 2;

    private const byte RejectedOutcome = 0;

    /// <summary>The zeroed 130-byte payload the case-4000 <c>1601</c> emission carries (treated read-only downstream).</summary>
    private static readonly byte[] ZeroTData = new byte[CenterWorldEventSorts.PayloadSize];

    /// <summary>
    ///     Ingest one authenticated op33 world-event. <paramref name="emitterSessionId" /> is the S2S link identity,
    ///     carried into any AntiCheat audit so a compromised/buggy zone is attributable.
    /// </summary>
    public async ValueTask IngestAsync(WorldEventInbound packet, long emitterSessionId,
        CancellationToken cancellationToken)
    {
        var sort = packet.Sort;
        var data = packet.Data;
        var nowUtc = DateTimeOffset.UtcNow;

        // --- Exceptions: dedicated, non-fan-out paths, handled BEFORE the fan-out allowlist gate. ---
        switch (sort)
        {
            case HeroRankPrePointSort: // 9998 -> hero-rank pre-point accrual, no fan-out.
            {
                var hPoint = ReadInt(data, 0);
                var hTribe = ReadInt(data, 4);
                var hLevel = ReadInt(data, 8);
                var uCharIdx = ReadInt(data, 12);

                heroRank.AddOrUpdate(hPoint, (byte)hTribe, hLevel, uCharIdx);
                return;
            }
            case CloseProxySort: // 10000 -> unicast op35 to a single zone, no fan-out.
            {
                var userIdx = ReadInt(data, 0);
                var charIdx = ReadInt(data, 4);
                var openUi = ReadInt(data, 8);
                var zoneNumber = ReadInt(data, 12);

                await closeProxy.SendCloseProxyAsync(zoneNumber, userIdx, charIdx, openUi, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }

        // --- Hardening gate: only recognized tSort proceed; unknown -> DROP + AntiCheat audit, never rebroadcast. ---
        if (!KnownTSortRegistry.IsKnown(sort))
        {
            Audit(DroppedUnknownTSortEventCode, sort, emitterSessionId, nowUtc, "tSort hors allowlist");
            return;
        }

        // --- Effect-of-state FIRST. false => the authority rejected an out-of-bounds index. ---
        if (!await ApplyEffectAsync(sort, data, cancellationToken).ConfigureAwait(false))
        {
            Audit(DroppedOutOfBoundsIndexEventCode, sort, emitterSessionId, nowUtc, "index hors borne");
            return;
        }

        // --- Fan-out LAST: re-emit op33 (same tSort + verbatim tData) to all zone links, emitter included. ---
        await broadcaster.BroadcastWorldEventAsync(sort, data, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Applies the effect-of-state for a recognized <paramref name="sort" /> by routing it to the authority.
    ///     Returns <see langword="false" /> only when the authority rejects an out-of-bounds array index; a
    ///     recognized-but-inert (fan-out-only) <paramref name="sort" /> returns <see langword="true" /> without
    ///     touching world state (the <c>default</c> arm — legacy's switch had no default, so every
    ///     recognized-but-unhandled case simply fanned out).
    /// </summary>
    private async ValueTask<bool> ApplyEffectAsync(int sort, byte[] data, CancellationToken cancellationToken)
    {
        int Read(int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        }

        switch (sort)
        {
            // ---- Zone049 (idx = tData[0-3]); cases 5-8 stamp real wall-clock (the authority owns the clock). ----
            case >= 2 and <= 9:
            {
                var value = (byte)(sort switch { 2 => 1, 3 => 2, 4 => 3, 5 => 5, 6 => 4, 7 => 4, 8 => 5, _ => 0 });
                var stampTime = sort is >= 5 and <= 8;
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone049, Read(0), value, stampTime);
            }

            // ---- Zone051 (idx = tData[0-3]). ----
            case >= 11 and <= 18:
            {
                var value = (byte)(sort switch { 11 => 1, 12 => 2, 13 => 3, 14 => 5, 15 => 5, 16 => 4, 17 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone051, Read(0), value, false);
            }

            // ---- Zone053 (idx = tData[0-3]). 19 (dead __GOD__ code) and 25/26/27 are inert -> default fan-out. ----
            case 20 or 21 or 22 or 23 or 24 or 28 or 29 or 30:
            {
                var value = (byte)(sort switch
                {
                    20 => 1, 21 => 2, 22 => 3, 23 => 5, 24 => 5, 28 => 4, 29 => 5, _ => 0
                });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone053, Read(0), value, false);
            }

            // ---- Tribe guardians (matrix, i1=tData[0-3], i2=tData[4-7]; int indices -> full bound check). ----
            case 31 or 32:
                return authority.SetTribeGuardState(Read(0), Read(4), (byte)(sort == 31 ? 0 : 1));

            // ---- Zone038 / tribe-symbol battle. ----
            case 38:
                return authority.SetSymbolWinTribe((byte)Read(0));
            case 40:
                authority.StartSymbolBattle();
                return true;
            case 42:
            {
                var symbolIndex = Read(0);
                var winTribe = (byte)Read(4);
                return symbolIndex switch
                {
                    >= 0 and <= 3 => authority.SetTribeSymbol(symbolIndex, winTribe),
                    4 => authority.SetMonsterSymbol(winTribe),
                    _ => false // invalid symbol sub-index -> DROP + audit
                };
            }
            case 45:
                authority.EndSymbolBattle();
                return true;

            // ---- Alliances (47 and 49 identical). Owner order: (tribe1, date1, tribe2, date2). ----
            case 46:
                return authority.SetAllianceState((byte)Read(0), (byte)Read(4));
            case 47 or 49:
                return authority.SetPossibleAlliance((byte)Read(0), Read(8), (byte)Read(4), Read(12));

            // ---- Tribe vote. Cases 52-56 are GLOBAL phase transitions across all four tribes (52 explicitly
            //      sets mTribeVoteState[0..3]); the owner API is per-tribe, so the phase is applied in a 0..3 loop. ----
            case 52:
                await SetGlobalVotePhaseAsync(1, purge: true, cancellationToken).ConfigureAwait(false);
                return true;
            case 53:
                SetGlobalVotePhase(2);
                return true;
            case 54:
                SetGlobalVotePhase(3);
                return true;
            case 55:
                SetGlobalVotePhase(4); // NOTE: per-tribe master election (case 55) has no dedicated owner method.
                return true;
            case 56:
                await SetGlobalVotePhaseAsync(0, purge: true, cancellationToken).ConfigureAwait(false);
                return true;
            case 58:
                await authority.ClearTribeVoteCandidateAsync((byte)Read(0), (byte)Read(4), cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 59:
                await authority.AddTribeVotePointsAsync((byte)Read(0), (byte)Read(4), Read(8), cancellationToken)
                    .ConfigureAwait(false);
                return true;
            case 61:
                return authority.ClearTribeSubMaster((byte)Read(0), (byte)Read(4));

            // Cases 57 (register candidate) and 60 (set sub-master) are BLOCKED (fan-out only, no effect): the op33
            // payload carries an avatar NAME at offset 8 whose length (MAX_AVATAR_NAME_LENGTH) is uncited in the
            // catalogue, and the owner's RegisterTribeVoteCandidateAsync expects a candidate CHARACTER ID that is
            // not present in the op33 envelope at all. See the handoff notes — needs analyst/owner reconciliation.

            // ---- Zone175 (matrix, i1=tData[0-3], i2=tData[4-7]); irregular value table; 110 -> 0. ----
            case >= 64 and <= 100:
            {
                var value = (byte)(sort switch
                {
                    64 => 1, 65 => 2, 66 => 3, 67 => 4, 68 => 5, 69 => 23, 70 => 23, 71 => 6, 72 => 23, 73 => 7,
                    74 => 8, 75 => 9, 76 => 23, 77 => 23, 78 => 10, 79 => 23, 80 => 11, 81 => 12, 82 => 13, 83 => 23,
                    84 => 23, 85 => 14, 86 => 23, 87 => 15, 88 => 16, 89 => 17, 90 => 23, 91 => 23, 92 => 18, 93 => 23,
                    94 => 19, 95 => 20, 96 => 21, 97 => 23, 98 => 23, 99 => 22, _ => 23 // 100
                });
                return authority.SetZone175TypeState(Read(0), Read(4), value);
            }
            case 110:
                return authority.SetZone175TypeState(Read(0), Read(4), 0);

            // ---- Zone194 scalar (index 0; 200 & 201 inert -> default). ----
            case >= 202 and <= 208:
            {
                var value = (byte)(sort switch { 202 => 1, 203 => 2, 204 => 3, 205 => 5, 206 => 4, 207 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone194, 0, value, false);
            }

            // ---- Zone194 bonus ratios (case 301 sub-dispatch); owner applies the x0.1 ratio scale internally. ----
            case 301:
            {
                var subSort = Read(0);
                var value = Read(4);
                switch (subSort)
                {
                    case 21: authority.SetTribeRatioBonus(TribeRatioBonusKind.GeneralExperienceUp, 0, value); break;
                    case 22: authority.SetTribeRatioBonus(TribeRatioBonusKind.GeneralExperienceUp, 1, value); break;
                    case 23: authority.SetTribeRatioBonus(TribeRatioBonusKind.GeneralExperienceUp, 2, value); break;
                    case 24: authority.SetTribeRatioBonus(TribeRatioBonusKind.GeneralExperienceUp, 3, value); break;
                    case 31: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUp, 0, value); break;
                    case 32: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUp, 1, value); break;
                    case 33: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUp, 2, value); break;
                    case 34: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUp, 3, value); break;
                    case 41: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUpForMyoung, 0, value); break;
                    case 42: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUpForMyoung, 1, value); break;
                    case 43: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUpForMyoung, 2, value); break;
                    case 44: authority.SetTribeRatioBonus(TribeRatioBonusKind.ItemDropUpForMyoung, 3, value); break;
                    case 51: authority.SetTribeKillOtherTribeAddValueExclusive(0, value); break;
                    case 52: authority.SetTribeKillOtherTribeAddValueExclusive(1, value); break;
                    case 53: authority.SetTribeKillOtherTribeAddValueExclusive(2, value); break;
                    case 54: authority.SetTribeKillOtherTribeAddValueExclusive(3, value); break;
                    // Unknown sub-sort: legacy's inner switch has no default -> fan-out only, no effect.
                }

                return true;
            }

            // ---- Case 302: master-call skill ability per tribe. ----
            case 302:
                return authority.SetTribeMasterCallAbility((byte)Read(0), Read(4));

            // ---- Zone267 (idx = tData[0-3]; 402 inert). ----
            case >= 403 and <= 410:
            {
                var value = (byte)(sort switch { 403 => 1, 404 => 2, 405 => 3, 406 => 5, 407 => 5, 408 => 4, 409 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone267, Read(0), value, false);
            }

            // ---- Zone241 "Den of Rebirth" (idx = tData[0-3]). ----
            case >= 411 and <= 415:
            {
                var value = (byte)(sort switch { 411 => 1, 412 => 2, 413 => 2, 414 => 2, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone241, Read(0), value, false);
            }

            // ---- FFA / Zone335 scalar (index 0; 1501 inert). ----
            case >= 1502 and <= 1507:
            {
                var value = (byte)(sort switch { 1502 => 1, 1503 => 2, 1504 => 3, 1505 => 4, 1506 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.ZoneFfa335, 0, value, false);
            }

            // ---- Case 1510: per-tribe DTM value. ----
            case 1510:
                return authority.SetTribeDtmValue((byte)Read(0), Read(4));

            // ---- Case 4000: emit zeroed 1601 FFA-refresh to all zones FIRST, then fall through to the normal
            //      fan-out of 4000 performed by the caller (double output). No state effect of its own. ----
            case 4000:
                await broadcaster.BroadcastWorldEventAsync(CenterWorldEventSorts.FfaRefresh, ZeroTData, cancellationToken)
                    .ConfigureAwait(false);
                return true;

            // Recognized-but-inert (fan-out-only) tSort: 1, 10, 19, 25-27, 33-37, 39, 41, 43-44, 48, 50-51, 57, 60,
            // 62-63, 101-109, 111-115, 200-201, 402, 416/418-428/500, sieges, tournament, 1133, 1501, 1511-1514, 1520.
            default:
                return true;
        }
    }

    private void SetGlobalVotePhase(byte phase)
    {
        for (var tribe = 0; tribe < TribeCount; tribe++)
            authority.SetTribeVoteState(tribe, phase);
    }

    private async ValueTask SetGlobalVotePhaseAsync(byte phase, bool purge, CancellationToken cancellationToken)
    {
        for (byte tribe = 0; tribe < TribeCount; tribe++)
        {
            authority.SetTribeVoteState(tribe, phase);
            if (purge)
                await authority.PurgeTribeVoteCandidatesAsync(tribe, cancellationToken).ConfigureAwait(false);
        }
    }

    private void Audit(short eventCode, int sort, long emitterSessionId, DateTimeOffset nowUtc, string reason)
    {
        var entry = new EventLogEntryTvp(
            eventCode,
            (byte)EventLogCategory.AntiCheat,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            sort, // reuse the ItemId column to carry the offending tSort (queryable)
            null,
            RejectedOutcome,
            $"op33 {reason}: tSort={sort}, lien S2S session={emitterSessionId}",
            nowUtc.UtcDateTime);

        if (!auditLog.Enqueue(entry))
            logger.LogWarning(
                "AntiCheat audit queue full; dropped op33 {Reason} record (tSort {Sort}, S2S link {SessionId})",
                reason, sort, emitterSessionId);
    }

    private static int ReadInt(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
    }
}
