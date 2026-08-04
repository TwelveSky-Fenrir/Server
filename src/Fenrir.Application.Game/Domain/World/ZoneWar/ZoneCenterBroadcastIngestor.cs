using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ZoneCenterBroadcastIngestor(
    ZoneCenterSiegeState state,
    ZoneRegistry zones,
    ILogger<ZoneCenterBroadcastIngestor> logger,
    WorldStateService? worldState = null,
    Lazy<ZoneEventBroadcaster>? worldReactions = null,
    Zone051Zone053SiegeState? zone051Zone053State = null,
    AllianceProposalCenterState? allianceState = null,
    IWorldEventUplink? uplink = null)
{
    public const int PayloadSize = 130;

    public const int Zone049RangeStart = 1;

    public const int Zone049RangeEnd = 9;

    public const int Zone175RangeStart = 64;
    public const int Zone175RangeEnd = 100;
    public const int Zone175ResetEventCode = 110;

    public const int Zone267RangeStart = 402;

    public const int Zone267RangeEnd = 410;

    public const int Zone241RangeStart = 411;

    public const int Zone241RangeEnd = 415;

    public const int Zone335RangeStart = 1501;

    public const int Zone335RangeEnd = 1507;

    public const int Zone194RangeStart = 202;

    public const int Zone194RangeEnd = 208;

    public const int Zone038WinEventCode = ScheduledZoneCenterEventCodes.Zone038WinEventCode;

    public const int TribeSymbolBattleCountdownEventCode = 39;

    public const int TribeSymbolBattleStartEventCode = 40;

    public const int TribeSymbolResolvedEventCode = 42;

    public const int TribeSymbolBattleEndEventCode = 45;

    public const int TribeMasterCallAbilityEventCode = 302;

    public const int DtmEventCode = 1510;

    public const int PingEventCode = 4000;

    public const int AllianceStoneCaptureLockoutDays = 28;

    private const int MonsterSymbolSlot = WorldStateService.TribeCount;

    private const int AllianceStoneNameOffset = 24;

    private const int AllianceStoneNameSize = 13;

    public void Ingest(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        if (!Allow(eventCode, nameof(Ingest)))
            return;

        if (!ApplyStateEffect(eventCode, data))
            return;

        if (eventCode == PingEventCode)
            BroadcastAllZonesPing();

        Relay(eventCode, data);

        if (KnownTSortRegistry.CrossesShardBoundary(eventCode))
            EnqueueForOtherShards(eventCode, data);
    }

    public void AnnounceAllianceStoneDestroyed(byte stoneTribe, byte lastAttackerTribe, string lastAttackerName)
    {
        if (!AllianceProposalCenterState.IsValidTribe(stoneTribe))
            return;

        if (!TryResolveAllianceHolders(stoneTribe, out var holderA, out var holderB))
            return;

        if (!GameDate.TryAddDays(GameDate.Today(), AllianceStoneCaptureLockoutDays, out var expiryDate))
        {
            logger.LogWarning(
                "Alliance-stone capture on tribe slot {StoneTribe} could not project a {Days}-day lockout date -- dropped without relay",
                stoneTribe, AllianceStoneCaptureLockoutDays);
            return;
        }

        Span<byte> payload = stackalloc byte[PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, holderA);
        BinaryPrimitives.WriteInt32LittleEndian(payload[4..], holderB);
        BinaryPrimitives.WriteInt32LittleEndian(payload[8..], expiryDate);
        BinaryPrimitives.WriteInt32LittleEndian(payload[12..], expiryDate);
        BinaryPrimitives.WriteInt32LittleEndian(payload[16..], stoneTribe);
        BinaryPrimitives.WriteInt32LittleEndian(payload[20..], lastAttackerTribe);
        LegacyWireCodec.WriteFixedString(payload.Slice(AllianceStoneNameOffset, AllianceStoneNameSize),
            lastAttackerName);

        Ingest(AllianceProposalCenterEventMap.BreakAllianceViaStoneCaptureEventCode, payload);
    }

    private bool TryResolveAllianceHolders(byte stoneTribe, out byte holderA, out byte holderB)
    {
        holderA = stoneTribe;
        holderB = 0;

        if (allianceState is { } shadow)
            for (var slot = 0; slot < AllianceProposalCenterState.SlotCount; slot++)
            {
                var (cellA, cellB) = shadow.GetSlot(slot);
                if (cellA is not { } tribeA || cellB is not { } tribeB)
                    continue;

                if (tribeA != stoneTribe && tribeB != stoneTribe)
                    continue;

                holderA = tribeA;
                holderB = tribeB;
                return true;
            }

        if (worldState?.GetAllyOf(stoneTribe) is not { } allyTribe)
            return false;

        holderB = allyTribe;
        return true;
    }

    public void ApplyRelayedEvent(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        if (!Allow(eventCode, nameof(ApplyRelayedEvent)))
            return;

        if (!ApplyStateEffect(eventCode, data))
            return;

        if (eventCode == PingEventCode)
            BroadcastAllZonesPing();

        Relay(eventCode, data);
    }

    private bool Allow(int eventCode, string entryPoint)
    {
        if (KnownTSortRegistry.IsKnown(eventCode))
            return true;

        logger.LogWarning(
            "Zone-center event sort {Sort} rejected at {EntryPoint}: not in the known-sort allowlist -- dropped without relay",
            eventCode, entryPoint);

        return false;
    }

    private void EnqueueForOtherShards(int eventCode, ReadOnlySpan<byte> data)
    {
        uplink?.Publish(eventCode, data);
    }

    private bool ApplyStateEffect(int eventCode, ReadOnlySpan<byte> data)
    {
        switch (eventCode)
        {
            case >= Zone049RangeStart and <= Zone049RangeEnd:
                return ApplyZone049(eventCode, data);

            case Zone175ResetEventCode:
                return ApplyZone175(eventCode, data, true);

            case >= Zone175RangeStart and <= Zone175RangeEnd:
                return ApplyZone175(eventCode, data, false);

            case >= Zone267RangeStart and <= Zone267RangeEnd:
                return ApplyZone267(eventCode, data);

            case >= Zone241RangeStart and <= Zone241RangeEnd:
                return ApplyZone241(eventCode, data);

            case >= Zone051Zone053BroadcastResolver.Zone051RangeStart
                and <= Zone051Zone053BroadcastResolver.Zone051RangeEnd
                when zone051Zone053State is not null:
                return Zone051Zone053BroadcastResolver.ApplyZone051(zone051Zone053State, eventCode, data, logger);

            case >= Zone051Zone053BroadcastResolver.Zone053RangeStart
                and <= Zone051Zone053BroadcastResolver.Zone053RangeEnd
                when zone051Zone053State is not null:
                return Zone051Zone053BroadcastResolver.ApplyZone053(zone051Zone053State, eventCode, data, logger);

            case >= Zone194RangeStart and <= Zone194RangeEnd:
                if (SiegeEventStateMap.TryMapZone194(eventCode, out var zone194State))
                    state.SetZone194State(zone194State);
                return true;

            case TribeMasterCallAbilityEventCode:
                return ApplyTribeMasterCallAbility(data);

            case DtmEventCode:
                return ApplyDtm(data);

            case >= Zone335RangeStart and <= Zone335RangeEnd:
                if (SiegeEventStateMap.TryMapZone335(eventCode, out var ffaState))
                    state.SetZone335(ffaState);
                return true;

            case TribeBonusRatioEventMap.EventCode:
                return TribeBonusRatioEventMap.Apply(state, data, logger);

            case >= AllianceProposalCenterEventMap.EventCodeRangeStart
                and <= AllianceProposalCenterEventMap.EventCodeRangeEnd:
                return ApplyAllianceEvent(eventCode, data);

            case PingEventCode:
                HsbRewardFlagResetReactor.Apply(zones);
                return true;

            case Zone038WinEventCode or TribeSymbolBattleCountdownEventCode or TribeSymbolBattleStartEventCode
                or TribeSymbolResolvedEventCode or TribeSymbolBattleEndEventCode:
                return ApplyWorldReaction(eventCode, data);

            default:
                return true;
        }
    }

    private bool ApplyAllianceEvent(int eventCode, ReadOnlySpan<byte> data)
    {
        if (!TryValidateWorldReactionIndices(eventCode, data))
            return false;

        if (allianceState is not null &&
            !AllianceProposalCenterEventMap.Apply(eventCode, data, allianceState, logger))
            return false;

        if (eventCode is AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode
            or AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode
            or AllianceProposalCenterEventMap.BreakAllianceViaStoneCaptureEventCode)
            worldReactions?.Value.ApplyRelayedStateAndReactions(eventCode, data);

        return true;
    }

    private bool ApplyWorldReaction(int eventCode, ReadOnlySpan<byte> data)
    {
        if (!TryValidateWorldReactionIndices(eventCode, data))
            return false;

        worldReactions?.Value.ApplyRelayedStateAndReactions(eventCode, data);
        return true;
    }

    private bool TryValidateWorldReactionIndices(int eventCode, ReadOnlySpan<byte> data)
    {
        switch (eventCode)
        {
            case Zone038WinEventCode:
                return ValidateTribe(eventCode, ReadInt32(data, 0));

            case TribeSymbolResolvedEventCode:
            {
                var symbolSlot = ReadInt32(data, 0);

                if (symbolSlot is < 0 or > MonsterSymbolSlot)
                {
                    logger.LogWarning(
                        "Tribe-symbol resolution event referenced out-of-range symbol slot {SymbolSlot} -- ignored, dropped without relay",
                        symbolSlot);
                    return false;
                }

                return ValidateTribe(eventCode, ReadInt32(data, 4));
            }

            case AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode
                or AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode
                or AllianceProposalCenterEventMap.BreakAllianceViaStoneCaptureEventCode:
            {
                var tribeA = ReadInt32(data, 0);
                var tribeB = ReadInt32(data, 4);

                if (!ValidateTribe(eventCode, tribeA) || !ValidateTribe(eventCode, tribeB))
                    return false;

                if (eventCode == AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode && tribeA == tribeB)
                {
                    logger.LogWarning(
                        "Alliance formation event named tribe {TribeId} on both sides -- ignored, dropped without relay",
                        tribeA);
                    return false;
                }

                return true;
            }

            default:
                return true;
        }
    }

    private bool ValidateTribe(int eventCode, int tribeId)
    {
        if (ZoneCenterSiegeState.IsValidTribe(tribeId))
            return true;

        logger.LogWarning(
            "Zone-center event {EventCode} referenced out-of-range tribe id {TribeId} -- ignored, dropped without relay",
            eventCode, tribeId);
        return false;
    }

    private bool ApplyZone049(int eventCode, ReadOnlySpan<byte> data)
    {
        if (eventCode == Zone049RangeStart)
            return true;

        var slot = ReadInt32(data, 0);
        if (!ZoneCenterSiegeState.IsValidZone049Slot(slot))
        {
            logger.LogWarning(
                "Zone049 sub-code {EventCode} referenced out-of-range slot {Slot} -- ignored, dropped without relay",
                eventCode, slot);
            return false;
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
        return true;
    }

    private bool ApplyZone175(int eventCode, ReadOnlySpan<byte> data, bool isReset)
    {
        var instance = ReadInt32(data, 0);
        var slot = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidZone175Cell(instance, slot))
        {
            logger.LogWarning(
                "Zone175 event {EventCode} referenced out-of-range instance {Instance}/slot {Slot} -- ignored, dropped without relay",
                eventCode, instance, slot);
            return false;
        }

        if (isReset)
            state.ResetZone175(instance, slot);
        else if (SiegeEventStateMap.TryMapZone175(eventCode, out var mapped))
            state.SetZone175(instance, slot, mapped);

        return true;
    }

    private bool ApplyZone267(int eventCode, ReadOnlySpan<byte> data)
    {
        var tribeIndex = ReadInt32(data, 0);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeIndex))
        {
            logger.LogWarning(
                "Zone267 event {EventCode} referenced out-of-range tribe index {TribeIndex} -- ignored, dropped without relay",
                eventCode, tribeIndex);
            return false;
        }

        if (SiegeEventStateMap.TryMapZone267(eventCode, out var mapped))
            state.SetZone267((byte)tribeIndex, mapped);

        return true;
    }

    private bool ApplyZone241(int eventCode, ReadOnlySpan<byte> data)
    {
        var instance = ReadInt32(data, 0);

        if (!ZoneCenterSiegeState.IsValidZone241Instance(instance))
        {
            logger.LogWarning(
                "Zone241 event {EventCode} referenced out-of-range instance {Instance} -- ignored, dropped without relay",
                eventCode, instance);
            return false;
        }

        if (SiegeEventStateMap.TryMapZone241(eventCode, out var challengeState))
            state.SetZone241(instance, challengeState);

        return true;
    }

    private bool ApplyTribeMasterCallAbility(ReadOnlySpan<byte> data)
    {
        var tribeId = ReadInt32(data, 0);
        var formationCode = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeId))
        {
            logger.LogWarning(
                "Tribe-master call-ability event referenced out-of-range tribe id {TribeId} -- ignored, dropped without relay",
                tribeId);
            return false;
        }

        if (formationCode is < 0 or > byte.MaxValue)
        {
            logger.LogWarning(
                "Tribe-master call-ability event for tribe {TribeId} carried out-of-range formation code {Code} -- ignored, dropped without relay",
                tribeId, formationCode);
            return false;
        }

        if (worldState is null)
        {
            logger.LogWarning(
                "Tribe-master call-ability event for tribe {TribeId} dropped without relay: no WorldStateService registered",
                tribeId);
            return false;
        }

        worldState.SetTribeFormationAbility((byte)tribeId, (byte)formationCode);
        return true;
    }

    private bool ApplyDtm(ReadOnlySpan<byte> data)
    {
        var tribeId = ReadInt32(data, 0);
        var effectValue = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeId))
        {
            logger.LogWarning("DTM event referenced out-of-range tribe id {TribeId} -- ignored, dropped without relay",
                tribeId);
            return false;
        }

        state.SetZone038DtmValue((byte)tribeId, effectValue);
        return true;
    }

    private void BroadcastAllZonesPing()
    {
        var empty = new byte[PayloadSize];
        var response = new ZoneEventInfoResponse
            { Sort = ScheduledZoneCenterEventCodes.HsbRewardFlagResetPingEventCode, Data = empty };

        BroadcastToEveryZone(in response);
    }

    private void Relay(int eventCode, ReadOnlySpan<byte> data)
    {
        var response = new ZoneEventInfoResponse { Sort = eventCode, Data = data.ToArray() };

        BroadcastToEveryZone(in response);
    }

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
                    if (player.Session is { } clientSession)
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
