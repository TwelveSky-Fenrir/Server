using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Cluster.Client.Link;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Protocol.Center;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ZoneCenterBroadcastIngestor(
    ZoneCenterSiegeState state,
    ZoneRegistry zones,
    ILogger<ZoneCenterBroadcastIngestor> logger,
    IRvrSiegeEventRelayQueue? relayQueue = null,
    IOptions<GameServerOptions>? gameOptions = null,
    Zone051Zone053SiegeState? zone051Zone053State = null,
    AllianceProposalCenterState? allianceState = null,
    Lazy<ICenterLink>? centerLink = null)
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

    public const int DtmEventCode = 1510;

    public const int PingEventCode = 4000;

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
        if (gameOptions?.Value.WorldStateAuthority == WorldStateAuthorityMode.Center)
        {
            centerLink?.Value.Send(new WorldEventOutbound { Sort = eventCode, Data = data.ToArray() });
            return;
        }

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
