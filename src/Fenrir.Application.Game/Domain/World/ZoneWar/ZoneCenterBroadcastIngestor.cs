using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ZoneCenterBroadcastIngestor(
    ZoneCenterSiegeState state,
    ZoneRegistry zones,
    ILogger<ZoneCenterBroadcastIngestor> logger,
    WorldState.WorldStateService? worldState = null,
    System.Lazy<ZoneEventBroadcaster>? worldReactions = null,
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

    public const int TribeMasterCallAbilityEventCode = 302;

    public const int DtmEventCode = 1510;

    public const int PingEventCode = 4000;

    public void Ingest(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        if (!Allow(eventCode, nameof(Ingest)))
            return;

        ApplyStateEffect(eventCode, data);

        if (eventCode == PingEventCode)
            BroadcastAllZonesPing();

        Relay(eventCode, data);

        if (eventCode is >= Zone049RangeStart and <= Zone049RangeEnd)
            EnqueueForOtherShards(eventCode, data);
    }

    public void ApplyRelayedEvent(int eventCode, ReadOnlySpan<byte> data)
    {
        if (data.Length != PayloadSize)
            throw new ArgumentException($"Zone-center broadcast payload must be exactly {PayloadSize} bytes.",
                nameof(data));

        if (!Allow(eventCode, nameof(ApplyRelayedEvent)))
            return;

        ApplyStateEffect(eventCode, data);
        Relay(eventCode, data);
    }

    // Le center legacy tombait dans son default: pour tout tSort inconnu, sans jamais relayer. Ici le relais
    // rediffuse a TOUS les joueurs connectes : laisser passer un code arbitraire ferait de ce chemin un
    // amplificateur de broadcast pilotable par le pair.
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

            case >= Zone051Zone053BroadcastResolver.Zone051RangeStart
                and <= Zone051Zone053BroadcastResolver.Zone051RangeEnd
                when zone051Zone053State is not null:
                Zone051Zone053BroadcastResolver.ApplyZone051(zone051Zone053State, eventCode, data, logger);
                break;

            case >= Zone051Zone053BroadcastResolver.Zone053RangeStart
                and <= Zone051Zone053BroadcastResolver.Zone053RangeEnd
                when zone051Zone053State is not null:
                Zone051Zone053BroadcastResolver.ApplyZone053(zone051Zone053State, eventCode, data, logger);
                break;

            case >= Zone194RangeStart and <= Zone194RangeEnd:
                if (SiegeEventStateMap.TryMapZone194(eventCode, out var zone194State))
                    state.SetZone194State(zone194State);
                break;

            case TribeMasterCallAbilityEventCode:
                ApplyTribeMasterCallAbility(data);
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
                HsbRewardFlagResetReactor.Apply(zones);
                break;

            // Sorts dont l'effet d'etat et les reactions de monde vivent dans ZoneEventBroadcaster
            // (resummon de gardes, evictions, reset de rang). On delegue plutot que de dupliquer, mais le
            // ROUTAGE est ici : il n'existe plus qu'une seule porte d'entree pour un evenement relaye.
            case 38 or 39 or 40 or 42 or 45 or 46 or 47:
                worldReactions?.Value.ApplyRelayedStateAndReactions(eventCode, data);
                break;
        }
    }

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

        if (SiegeEventStateMap.TryMapZone267(eventCode, out var mapped))
            state.SetZone267((byte)tribeIndex, mapped);
    }

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

    // Le legacy n'a QU'UN tableau mTribeMasterCallAbility : ecrit ici sur tSort 302, purge sur tSort 45 par
    // la fin de bataille des symboles, lu par le combat. La cible est donc _tribeFormationAbility de
    // WorldStateService, pas un magasin de siege separe. Le legacy ne borne ni la tribu ni le code
    // (Server/ts25center/S04_MyWork02.cpp:924 ecrit dans un int[MAX_TRIBE_NUM]) : on borne les deux.
    private void ApplyTribeMasterCallAbility(ReadOnlySpan<byte> data)
    {
        var tribeId = ReadInt32(data, 0);
        var formationCode = ReadInt32(data, 4);

        if (!ZoneCenterSiegeState.IsValidTribe(tribeId))
        {
            logger.LogWarning(
                "Tribe-master call-ability event referenced out-of-range tribe id {TribeId} -- ignored", tribeId);
            return;
        }

        if (formationCode is < 0 or > byte.MaxValue)
        {
            logger.LogWarning(
                "Tribe-master call-ability event for tribe {TribeId} carried out-of-range formation code {Code} -- ignored",
                tribeId, formationCode);
            return;
        }

        if (worldState is null)
        {
            logger.LogWarning(
                "Tribe-master call-ability event for tribe {TribeId} dropped: no WorldStateService registered",
                tribeId);
            return;
        }

        worldState.SetTribeFormationAbility((byte)tribeId, (byte)formationCode);
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
