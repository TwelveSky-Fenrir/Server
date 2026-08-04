using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain.Progression;
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
    TribeGuardCorridorState tribeGuardState,
    PopupEventState popupState,
    WorldStateService? worldState = null,
    Lazy<ZoneEventBroadcaster>? worldReactions = null,
    Zone051Zone053SiegeState? zone051Zone053State = null,
    AllianceProposalCenterState? allianceState = null,
    IWorldEventUplink? uplink = null,
    TowerWarState? towerWar = null,
    SecondarySiegeEventAdmission? secondarySiegeEvents = null)
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

    public const int PopupStateEventCode = 1511;

    public const int PopupCountdownEventCode = 1512;

    public const int PopupCloseEventCode = 1513;

    public const int PingEventCode = 4000;

    public const int AllianceStoneCaptureLockoutDays = 28;

    private const int MonsterSymbolSlot = WorldStateService.TribeCount;

    private const int AllianceStoneNameOffset = 24;

    private const int AllianceStoneNameSize = 13;

    private static readonly SecondarySiegeEventAdmission DisabledSecondarySiegeEvents = new(false);

    public bool Ingest(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        if (!Allow(eventCode, nameof(Ingest)))
            return false;

        if (!ApplyStateEffect(eventCode, data, apply: false))
            return false;

        if (KnownTSortRegistry.CrossesShardBoundary(eventCode) && !TryEnqueueForOtherShards(eventCode, data))
            return false;

        if (!ApplyStateEffect(eventCode, data, apply: true))
        {
            logger.LogError(
                "Zone-center event sort {Sort} was accepted by the durable relay but could not be applied locally",
                eventCode);
            return false;
        }

        if (eventCode == PingEventCode)
            BroadcastAllZonesPing();

        Relay(eventCode, data);
        return true;
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

        if (!ApplyStateEffect(eventCode, data, apply: true))
            return;

        if (eventCode == PingEventCode)
            BroadcastAllZonesPing();

        Relay(eventCode, data);
    }

    private bool Allow(int eventCode, string entryPoint)
    {
        if (!(secondarySiegeEvents ?? DisabledSecondarySiegeEvents).Allows(eventCode))
        {
            logger.LogWarning(
                "Zone-center event sort {Sort} rejected at {EntryPoint}: secondary siege content is disabled -- dropped without state mutation or relay",
                eventCode, entryPoint);
            return false;
        }

        if (KnownTSortRegistry.IsKnown(eventCode))
            return true;

        logger.LogWarning(
            "Zone-center event sort {Sort} rejected at {EntryPoint}: not in the known-sort allowlist -- dropped without relay",
            eventCode, entryPoint);

        return false;
    }

    private bool TryEnqueueForOtherShards(int eventCode, ReadOnlySpan<byte> data)
    {
        if (uplink is null)
        {
            logger.LogError(
                "Zone-center event sort {Sort} rejected before local state mutation: durable relay services are unavailable",
                eventCode);
            return false;
        }

        var identity = WorldEventPublicationIdentity.Create();
        WorldEventUplinkResult result;
        try
        {
            result = uplink.Publish(eventCode, data, identity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Zone-center event sort {Sort} operation {OperationId} faulted before local state mutation",
                eventCode, identity.OperationId);
            return false;
        }

        if (result.IsEnqueued)
            return true;

        logger.LogWarning(
            "Zone-center event sort {Sort} operation {OperationId} rejected before local state mutation: durable relay returned {Result}",
            eventCode, result.Identity.OperationId, result.Kind);
        return false;
    }

    private bool ApplyStateEffect(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        switch (eventCode)
        {
            case >= Zone049RangeStart and <= Zone049RangeEnd:
                return ApplyZone049(eventCode, data, apply);

            case Zone175ResetEventCode:
                return ApplyZone175(eventCode, data, true, apply);

            case >= Zone175RangeStart and <= Zone175RangeEnd:
                return ApplyZone175(eventCode, data, false, apply);

            case >= Zone267RangeStart and <= Zone267RangeEnd:
                return ApplyZone267(eventCode, data, apply);

            case >= Zone241RangeStart and <= Zone241RangeEnd:
                return ApplyZone241(eventCode, data, apply);

            case >= Zone051Zone053BroadcastResolver.Zone051RangeStart
                and <= Zone051Zone053BroadcastResolver.Zone051RangeEnd
                when zone051Zone053State is not null:
                return ApplyZone051(eventCode, data, apply);

            case >= Zone051Zone053BroadcastResolver.Zone053RangeStart
                and <= Zone051Zone053BroadcastResolver.Zone053RangeEnd
                when zone051Zone053State is not null:
                return ApplyZone053(eventCode, data, apply);

            case TribeGuardCorridorState.PassageOpenedEventCode or TribeGuardCorridorState.PassageBlockedEventCode:
                return ApplyTribeGuardCorridor(eventCode, data, apply);

            case TowerWarEventCodes.TowerState:
                return ApplyTowerStatus(data, apply);

            case >= Zone194RangeStart and <= Zone194RangeEnd:
                if (apply && SiegeEventStateMap.TryMapZone194(eventCode, out var zone194State))
                    state.SetZone194State(zone194State);
                return true;

            case TribeMasterCallAbilityEventCode:
                return ApplyTribeMasterCallAbility(data, apply);

            case DtmEventCode:
                return ApplyDtm(data, apply);

            case PopupStateEventCode:
                return ApplyPopupState(data, apply);

            case PopupCountdownEventCode:
                return ApplyPopupCountdown(data, apply);

            case PopupCloseEventCode:
                return ApplyPopupClose(data, apply);

            case >= Zone335RangeStart and <= Zone335RangeEnd:
                if (apply && SiegeEventStateMap.TryMapZone335(eventCode, out var ffaState))
                    state.SetZone335(ffaState);
                return true;

            case TribeBonusRatioEventMap.EventCode:
                return !apply || TribeBonusRatioEventMap.Apply(state, data, logger);

            case >= AllianceProposalCenterEventMap.EventCodeRangeStart
                and <= AllianceProposalCenterEventMap.EventCodeRangeEnd:
                return ApplyAllianceEvent(eventCode, data, apply);

            case PingEventCode:
                if (apply)
                    HsbRewardFlagResetReactor.Apply(zones);
                return true;

            case Zone038WinEventCode or TribeSymbolBattleCountdownEventCode or TribeSymbolBattleStartEventCode
                or TribeSymbolResolvedEventCode or TribeSymbolBattleEndEventCode:
                return ApplyWorldReaction(eventCode, data, apply);

            default:
                return true;
        }
    }

    private bool ApplyTowerStatus(ReadOnlySpan<byte> data, bool apply)
    {
        if (!TowerStatusRelaySnapshot.TryRead(data, out var snapshot))
        {
            logger.LogWarning(
                "Tower status event {EventCode} carried an invalid tower snapshot -- ignored, dropped without relay",
                TowerWarEventCodes.TowerState);
            return false;
        }

        if (towerWar is null)
        {
            logger.LogWarning(
                "Tower status event {EventCode} dropped without relay: no TowerWarState is registered",
                TowerWarEventCodes.TowerState);
            return false;
        }

        if (apply)
            snapshot.ApplyTo(towerWar);
        return true;
    }

    private bool ApplyAllianceEvent(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        if (!TryValidateWorldReactionIndices(eventCode, data))
            return false;

        if (apply && allianceState is not null &&
            !AllianceProposalCenterEventMap.Apply(eventCode, data, allianceState, logger))
            return false;

        if (apply && eventCode is AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode
            or AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode
            or AllianceProposalCenterEventMap.BreakAllianceViaStoneCaptureEventCode)
            worldReactions?.Value.ApplyRelayedStateAndReactions(eventCode, data);

        return true;
    }

    private bool ApplyWorldReaction(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        if (!TryValidateWorldReactionIndices(eventCode, data))
            return false;

        if (apply)
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

    private bool ApplyZone049(int eventCode, ReadOnlySpan<byte> data, bool apply)
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

        if (apply)
            state.SetZone049State(slot, value, stampTime);
        return true;
    }

    private bool ApplyZone175(int eventCode, ReadOnlySpan<byte> data, bool isReset, bool apply)
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

        if (!apply)
            return true;

        if (isReset)
            state.ResetZone175(instance, slot);
        else if (SiegeEventStateMap.TryMapZone175(eventCode, out var mapped))
            state.SetZone175(instance, slot, mapped);

        return true;
    }

    private bool ApplyZone267(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        var tribeIndex = ReadInt32(data, 0);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeIndex))
        {
            logger.LogWarning(
                "Zone267 event {EventCode} referenced out-of-range tribe index {TribeIndex} -- ignored, dropped without relay",
                eventCode, tribeIndex);
            return false;
        }

        if (apply && SiegeEventStateMap.TryMapZone267(eventCode, out var mapped))
            state.SetZone267((byte)tribeIndex, mapped);

        return true;
    }

    private bool ApplyTribeGuardCorridor(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        var tribeId = ReadInt32(data, 0);
        var segmentIndex = ReadInt32(data, sizeof(int));

        if (tribeId is < 0 or >= TribeGuardCorridorState.TribeCount ||
            segmentIndex is < 0 or >= TribeGuardCorridorState.SegmentCount)
        {
            logger.LogWarning(
                "Tribe-guard passage event {EventCode} referenced out-of-range tribe {TribeId}/segment {SegmentIndex} -- ignored, dropped without relay",
                eventCode, tribeId, segmentIndex);
            return false;
        }

        if (apply)
            tribeGuardState.TrySetOpen((byte)tribeId, (byte)segmentIndex,
                eventCode == TribeGuardCorridorState.PassageOpenedEventCode);
        return true;
    }

    private bool ApplyZone241(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        var instance = ReadInt32(data, 0);

        if (!ZoneCenterSiegeState.IsValidZone241Instance(instance))
        {
            logger.LogWarning(
                "Zone241 event {EventCode} referenced out-of-range instance {Instance} -- ignored, dropped without relay",
                eventCode, instance);
            return false;
        }

        if (apply && SiegeEventStateMap.TryMapZone241(eventCode, out var challengeState))
            state.SetZone241(instance, challengeState);

        return true;
    }

    private bool ApplyZone051(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        var slot = ReadInt32(data, 0);
        if (!Zone051Zone053SiegeState.IsValidZone051Slot(slot))
        {
            logger.LogWarning(
                "Zone051 selector {Selector} referenced out-of-range slot {Slot} -- ignored, dropped without relay",
                eventCode, slot);
            return false;
        }

        return !apply || Zone051Zone053BroadcastResolver.ApplyZone051(zone051Zone053State!, eventCode, data, logger);
    }

    private bool ApplyZone053(int eventCode, ReadOnlySpan<byte> data, bool apply)
    {
        var slot = ReadInt32(data, 0);
        if (!Zone051Zone053SiegeState.IsValidZone053Slot(slot))
        {
            logger.LogWarning(
                "Zone053 selector {Selector} referenced out-of-range slot {Slot} -- ignored, dropped without relay",
                eventCode, slot);
            return false;
        }

        return !apply || Zone051Zone053BroadcastResolver.ApplyZone053(zone051Zone053State!, eventCode, data, logger);
    }

    private bool ApplyTribeMasterCallAbility(ReadOnlySpan<byte> data, bool apply)
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

        if (apply)
            worldState.SetTribeFormationAbility((byte)tribeId, (byte)formationCode);
        return true;
    }

    private bool ApplyDtm(ReadOnlySpan<byte> data, bool apply)
    {
        var tribeId = ReadInt32(data, 0);
        var effectValue = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeId))
        {
            logger.LogWarning("DTM event referenced out-of-range tribe id {TribeId} -- ignored, dropped without relay",
                tribeId);
            return false;
        }

        if (effectValue is < 0 or > 3)
        {
            logger.LogWarning("DTM event referenced out-of-range effect {EffectValue} -- ignored, dropped without relay",
                effectValue);
            return false;
        }

        if (apply)
        {
            state.SetZone038DtmValue((byte)tribeId, effectValue);
            zones.ApplyZone38TribeEffects(state.CaptureZone38TribeEffects());
        }
        return true;
    }

    private bool ApplyPopupState(ReadOnlySpan<byte> data, bool apply)
    {
        var enabled = ReadInt32(data, 0);
        var typeValue = ReadInt32(data, sizeof(int));
        var pvpRequirement = ReadInt32(data, sizeof(int) * 2);
        var pvmRequirement = ReadInt32(data, sizeof(int) * 3);

        if (!TryResolvePopupType(typeValue, PopupStateEventCode, out var type) ||
            enabled is < 0 or > 1 || pvpRequirement < 0 || pvmRequirement < 0)
        {
            logger.LogWarning(
                "Popup state event carried invalid values: state={State} type={Type} pvp={PvpRequirement} pvm={PvmRequirement} -- ignored, dropped without relay",
                enabled, typeValue, pvpRequirement, pvmRequirement);
            return false;
        }

        if (apply)
            popupState.ApplyState(type, enabled != 0, pvpRequirement, pvmRequirement);
        return true;
    }

    private bool ApplyPopupCountdown(ReadOnlySpan<byte> data, bool apply)
    {
        var typeValue = ReadInt32(data, 0);
        var remainingMinutes = ReadInt32(data, sizeof(int));

        if (!TryResolvePopupType(typeValue, PopupCountdownEventCode, out var type))
            return false;

        if (apply)
            popupState.SetCountdown(type, remainingMinutes);
        return true;
    }

    private bool ApplyPopupClose(ReadOnlySpan<byte> data, bool apply)
    {
        var typeValue = ReadInt32(data, 0);
        if (!TryResolvePopupType(typeValue, PopupCloseEventCode, out var type))
            return false;

        if (apply)
            popupState.SetCountdown(type, 0);
        return true;
    }

    private bool TryResolvePopupType(int typeValue, int eventCode, out PopupEventType type)
    {
        if (typeValue is >= 0 and < 5)
        {
            type = (PopupEventType)typeValue;
            return true;
        }

        logger.LogWarning(
            "Popup event {EventCode} referenced out-of-range type {PopupType} -- ignored, dropped without relay",
            eventCode, typeValue);
        type = default;
        return false;
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
        if (eventCode == TowerWarEventCodes.TowerState && TowerStatusRelaySnapshot.TryRead(data, out var snapshot))
        {
            var towerResponse = snapshot.ToResponse();
            BroadcastToEveryZone(in towerResponse);
            return;
        }

        var response = new ZoneEventInfoResponse { Sort = eventCode, Data = data.ToArray() };

        if (ValleyWarCenterRelayCodes.IsValleyWarCampaignEvent(eventCode))
            BroadcastToValleyWarCampaign(in response);
        else
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

    private void BroadcastToValleyWarCampaign<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            {
                if (!ValleyWarMapCatalog.Contains(zone.MapId))
                    continue;

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
