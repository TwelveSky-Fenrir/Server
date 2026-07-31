using System.Buffers.Binary;
using Fenrir.Cluster.Center.WorldState;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Protocol.Center;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Center.EventBus;

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

    private static readonly byte[] ZeroTData = new byte[CenterWorldEventSorts.PayloadSize];

    public async ValueTask IngestAsync(WorldEventInbound packet, long emitterSessionId,
        CancellationToken cancellationToken)
    {
        var sort = packet.Sort;
        var data = packet.Data;
        var nowUtc = DateTimeOffset.UtcNow;

        switch (sort)
        {
            case HeroRankPrePointSort:
            {
                var hPoint = ReadInt(data, 0);
                var hTribe = ReadInt(data, 4);
                var hLevel = ReadInt(data, 8);
                var uCharIdx = ReadInt(data, 12);

                heroRank.AddOrUpdate(hPoint, (byte)hTribe, hLevel, uCharIdx);
                return;
            }
            case CloseProxySort:
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

        if (!KnownTSortRegistry.IsKnown(sort))
        {
            Audit(DroppedUnknownTSortEventCode, sort, emitterSessionId, nowUtc, "tSort hors allowlist");
            return;
        }

        if (!await ApplyEffectAsync(sort, data, cancellationToken).ConfigureAwait(false))
        {
            Audit(DroppedOutOfBoundsIndexEventCode, sort, emitterSessionId, nowUtc, "index hors borne");
            return;
        }

        await broadcaster.BroadcastWorldEventAsync(sort, data, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> ApplyEffectAsync(int sort, byte[] data, CancellationToken cancellationToken)
    {
        int Read(int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));
        }

        switch (sort)
        {
            case >= 2 and <= 9:
            {
                var value = (byte)(sort switch { 2 => 1, 3 => 2, 4 => 3, 5 => 5, 6 => 4, 7 => 4, 8 => 5, _ => 0 });
                var stampTime = sort is >= 5 and <= 8;
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone049, Read(0), value, stampTime);
            }

            case >= 11 and <= 18:
            {
                var value =
                    (byte)(sort switch { 11 => 1, 12 => 2, 13 => 3, 14 => 5, 15 => 5, 16 => 4, 17 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone051, Read(0), value, false);
            }

            case 20 or 21 or 22 or 23 or 24 or 28 or 29 or 30:
            {
                var value = (byte)(sort switch
                {
                    20 => 1, 21 => 2, 22 => 3, 23 => 5, 24 => 5, 28 => 4, 29 => 5, _ => 0
                });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone053, Read(0), value, false);
            }

            case 31 or 32:
                return authority.SetTribeGuardState(Read(0), Read(4), (byte)(sort == 31 ? 0 : 1));

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
                    _ => false
                };
            }
            case 45:
                authority.EndSymbolBattle();
                return true;

            case 46:
                return authority.SetAllianceState((byte)Read(0), (byte)Read(4));
            case 47 or 49:
                return authority.SetPossibleAlliance((byte)Read(0), Read(8), (byte)Read(4), Read(12));

            case 52:
                await SetGlobalVotePhaseAsync(1, true, cancellationToken).ConfigureAwait(false);
                return true;
            case 53:
                SetGlobalVotePhase(2);
                return true;
            case 54:
                SetGlobalVotePhase(3);
                return true;
            case 55:
                SetGlobalVotePhase(4);
                return true;
            case 56:
                await SetGlobalVotePhaseAsync(0, true, cancellationToken).ConfigureAwait(false);
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


            case >= 64 and <= 100:
            {
                var value = (byte)(sort switch
                {
                    64 => 1, 65 => 2, 66 => 3, 67 => 4, 68 => 5, 69 => 23, 70 => 23, 71 => 6, 72 => 23, 73 => 7,
                    74 => 8, 75 => 9, 76 => 23, 77 => 23, 78 => 10, 79 => 23, 80 => 11, 81 => 12, 82 => 13, 83 => 23,
                    84 => 23, 85 => 14, 86 => 23, 87 => 15, 88 => 16, 89 => 17, 90 => 23, 91 => 23, 92 => 18, 93 => 23,
                    94 => 19, 95 => 20, 96 => 21, 97 => 23, 98 => 23, 99 => 22, _ => 23
                });
                return authority.SetZone175TypeState(Read(0), Read(4), value);
            }
            case 110:
                return authority.SetZone175TypeState(Read(0), Read(4), 0);

            case >= 202 and <= 208:
            {
                var value = (byte)(sort switch { 202 => 1, 203 => 2, 204 => 3, 205 => 5, 206 => 4, 207 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone194, 0, value, false);
            }

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
                }

                return true;
            }

            case 302:
                return authority.SetTribeMasterCallAbility((byte)Read(0), Read(4));

            case >= 403 and <= 410:
            {
                var value = (byte)(sort switch
                {
                    403 => 1, 404 => 2, 405 => 3, 406 => 5, 407 => 5, 408 => 4, 409 => 5, _ => 0
                });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone267, Read(0), value, false);
            }

            case >= 411 and <= 415:
            {
                var value = (byte)(sort switch { 411 => 1, 412 => 2, 413 => 2, 414 => 2, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.Zone241, Read(0), value, false);
            }

            case >= 1502 and <= 1507:
            {
                var value = (byte)(sort switch { 1502 => 1, 1503 => 2, 1504 => 3, 1505 => 4, 1506 => 5, _ => 0 });
                return authority.SetZoneTypeState(WorldZoneStateKind.ZoneFfa335, 0, value, false);
            }

            case 1510:
                return authority.SetTribeDtmValue((byte)Read(0), Read(4));

            case 4000:
                await broadcaster
                    .BroadcastWorldEventAsync(CenterWorldEventSorts.FfaRefresh, ZeroTData, cancellationToken)
                    .ConfigureAwait(false);
                return true;

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
            sort,
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
