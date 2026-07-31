using Fenrir.Application.Game.Abstractions.World;
using System.Buffers;
using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class ZoneEventBroadcaster(
    WorldStateService worldState,
    ZoneRegistry zones,
    ILogger<ZoneEventBroadcaster> logger,
    TribeGuardSpawner? guardSpawner = null,
    TribeSymbolSpawner? symbolSpawner = null,
    ZoneCenterSiegeState? siegeState = null,
    Zone039ArmingReactor? zone039Reactor = null,
    IWorldEventUplink? uplink = null)
{
    private const int DataSize = 130;

    public void AnnounceZone038Winner(byte tribeId)
    {
        worldState.SetZone038Winner(tribeId);
        var data = Broadcast(38, tribeId);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceZone038WinnerResummon(zone);

        zone039Reactor?.Apply(zones);

        EnqueueForOtherShards(38, data);
    }

    public void AnnounceTribeSymbolBattleCountdown()
    {
        var data = Broadcast(39);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);

        foreach (var zone in zones.Zones)
            zone.PostHolyStoneCountdownEviction();

        EnqueueForOtherShards(39, data);
    }

    public void AnnounceTribeSymbolBattleStarted()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Started");

        worldState.StartTribeSymbolBattle();
        var data = Broadcast(40);

        if (symbolSpawner is not null)
            foreach (var zone in zones.Zones)
                symbolSpawner.EvaluateNow(zone);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);

        foreach (var zone in zones.Zones)
            zone.PostHolyStoneCountdownEviction();

        foreach (var zone in zones.Zones)
            zone.PostHolyStoneBattleRankReset();

        EnqueueForOtherShards(40, data);

        logger.LogInformation("Tribe symbol battle opened; every tribe reset to its own symbol");
    }

    public void AnnounceTribeSymbolBattleEnded()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Ended");

        worldState.EndTribeSymbolBattle();
        var data = Broadcast(45);
        EnqueueForOtherShards(45, data);

        logger.LogInformation("Tribe symbol battle closed");
    }

    public void AnnounceSymbolResolved(byte symbolIndex, byte winnerTribeId)
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolIndex} {WinnerTribeId}", symbolIndex, winnerTribeId);

        if (symbolIndex == WorldStateService.TribeCount)
            worldState.ResolveMonsterSymbol(winnerTribeId);
        else
            worldState.ResolveTribeSymbol(symbolIndex, winnerTribeId);

        var data = Broadcast(42, symbolIndex, winnerTribeId);
        EnqueueForOtherShards(42, data);

        logger.LogInformation("Symbol {SymbolIndex} resolved to tribe {WinnerTribeId}", symbolIndex, winnerTribeId);
    }

    public void AnnounceAllianceOffer(byte fromTribeId, byte toTribeId, bool isAccepted)
    {
        worldState.SetAllianceOffer(fromTribeId, toTribeId, isAccepted);
        var data = Broadcast(46, fromTribeId, toTribeId, isAccepted ? 1 : 0);
        EnqueueForOtherShards(46, data);
    }

    public void AnnounceAllianceDissolved(byte tribeA, byte tribeB)
    {
        worldState.DissolveAlliance(tribeA, tribeB);
        var data = Broadcast(47, tribeA, tribeB);
        EnqueueForOtherShards(47, data);
    }

    public void AnnounceMonsterSymbolAttackWindow()
    {
        Broadcast(401);
    }

    public void AnnounceTribePointTotals(IReadOnlyList<int> totals)
    {
        if (totals.Count != WorldStateService.TribeCount)
            throw new ArgumentException($"Expected exactly {WorldStateService.TribeCount} totals.", nameof(totals));

        Broadcast(1234, totals[0], totals[1], totals[2], totals[3]);
    }

    public void AnnounceTowerStatus(TowerWarState towerWar)
    {
        var response = towerWar.BuildStatusSnapshot();

        BroadcastToEveryZone(in response);
    }

    public void AnnounceFfaCountdown(int minutesRemaining)
    {
        Broadcast(1501, minutesRemaining);
    }

    public void AnnounceFfaGateOpen()
    {
        siegeState?.SetZone335(1);
        Broadcast(1502);
    }

    public void AnnounceFfaEntranceOpen()
    {
        siegeState?.SetZone335(2);
        Broadcast(1503);
    }

    public void AnnounceFfaBattleStart(int battleTimerLegacyTicks)
    {
        siegeState?.SetZone335(3);
        Broadcast(1504, battleTimerLegacyTicks);
    }

    public void AnnounceFfaBattleEnd()
    {
        siegeState?.SetZone335(4);
        Broadcast(1505);
    }

    public void AnnounceFfaClosedNotice()
    {
        siegeState?.SetZone335(5);
        Broadcast(1506);
    }

    public void AnnounceFfaReset()
    {
        siegeState?.ResetZone335();
        Broadcast(1507);
    }

    public void AnnounceValleyWarGateCountdown(int remainingCount)
    {
        Broadcast(659, remainingCount);
    }

    public void AnnounceValleyWarGateOpened()
    {
        Broadcast(660);
    }

    public void AnnounceValleyWarGateClosed()
    {
        Broadcast(662);
    }

    public void AnnounceValleyWarDoorOpened()
    {
        Broadcast(663);
    }

    public void AnnounceValleyWarTribeWin(byte winningTribe)
    {
        Broadcast(666, winningTribe);
    }

    public void AnnounceValleyWarBattleScrollDeleted()
    {
        Broadcast(667);
    }

    public void AnnounceValleyWarBossDefeated()
    {
        Broadcast(668);
    }

    public void AnnounceValleyWarReturnToTown()
    {
        Broadcast(669);
    }

    private byte[] Broadcast(int sort, params ReadOnlySpan<int> fields)
    {
        var data = new byte[DataSize];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), fields[i]);

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };

        BroadcastToEveryZone(in response);

        return data;
    }

    private void EnqueueForOtherShards(int sort, byte[] data)
    {
        uplink?.Publish(sort, data);
    }

    public void ApplyRelayedEvent(int sort, ReadOnlySpan<byte> data)
    {
        if (data.Length != DataSize)
            throw new ArgumentException($"RvR-siege relay payload must be exactly {DataSize} bytes.", nameof(data));

        switch (sort)
        {
            case 38:
                worldState.SetZone038Winner((byte)ReadInt32(data, 0));
                break;

            case 40:
                worldState.StartTribeSymbolBattle();
                break;

            case 42:
                var symbolIndex = (byte)ReadInt32(data, 0);
                var winnerTribeId = (byte)ReadInt32(data, 4);
                if (symbolIndex == WorldStateService.TribeCount)
                    worldState.ResolveMonsterSymbol(winnerTribeId);
                else
                    worldState.ResolveTribeSymbol(symbolIndex, winnerTribeId);
                break;

            case 45:
                worldState.EndTribeSymbolBattle();
                break;

            case 46:
                worldState.SetAllianceOffer((byte)ReadInt32(data, 0), (byte)ReadInt32(data, 4),
                    ReadInt32(data, 8) != 0);
                break;

            case 47:
                worldState.DissolveAlliance((byte)ReadInt32(data, 0), (byte)ReadInt32(data, 4));
                break;
        }

        var response = new ZoneEventInfoResponse { Sort = sort, Data = data.ToArray() };
        BroadcastToEveryZone(in response);

        switch (sort)
        {
            case 38:
                if (guardSpawner is not null)
                    foreach (var zone in zones.Zones)
                        guardSpawner.ForceZone038WinnerResummon(zone);

                zone039Reactor?.Apply(zones);
                break;

            case 39:
                if (guardSpawner is not null)
                    foreach (var zone in zones.Zones)
                        guardSpawner.ForceOrdinaryResummon(zone);

                foreach (var zone in zones.Zones)
                    zone.PostHolyStoneCountdownEviction();
                break;

            case 40:
                if (symbolSpawner is not null)
                    foreach (var zone in zones.Zones)
                        symbolSpawner.EvaluateNow(zone);

                if (guardSpawner is not null)
                    foreach (var zone in zones.Zones)
                        guardSpawner.ForceOrdinaryResummon(zone);

                foreach (var zone in zones.Zones)
                    zone.PostHolyStoneCountdownEviction();

                foreach (var zone in zones.Zones)
                    zone.PostHolyStoneBattleRankReset();
                break;
        }
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
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
                        "Cluster-wide RvR broadcast to character {RecipientId} (zone {MapId}) failed",
                        player.CharacterId, zone.MapId);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
