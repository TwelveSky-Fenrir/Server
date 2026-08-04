using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

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

    private const int NameFieldSize = 13;

    public void AnnounceHolyStoneOpeningCountdown(int minutesRemaining)
    {
        Broadcast(33, minutesRemaining);
    }

    public void AnnounceHolyStoneWarZoneOpen()
    {
        Broadcast(34);
    }

    public void AnnounceHolyStoneChallengerApproaching(byte tribeId, string characterName)
    {
        BroadcastWithName(35, tribeId, characterName);
    }

    public void AnnounceHolyStoneChallengeCancelled()
    {
        Broadcast(36);
    }

    public void AnnounceHolyStoneChallengeCountdown(int minutesRemaining)
    {
        Broadcast(37, minutesRemaining);
    }

    public void AnnounceZone038Winner(byte tribeId, string? capturerName = null)
    {
        var data = CreateDataWithName(tribeId, capturerName ?? string.Empty);
        if (!TryEnqueueForOtherShards(38, data))
            return;

        worldState.SetZone038Winner(tribeId);
        BroadcastPrepared(38, data);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceZone038WinnerResummon(zone);

        zone039Reactor?.Apply(zones);
    }

    public void AnnounceTribeSymbolBattleCountdown()
    {
        var data = CreateData();
        if (!TryEnqueueForOtherShards(39, data))
            return;

        BroadcastPrepared(39, data);

        if (guardSpawner is not null)
            foreach (var zone in zones.Zones)
                guardSpawner.ForceOrdinaryResummon(zone);

        foreach (var zone in zones.Zones)
            zone.PostHolyStoneCountdownEviction();
    }

    public void AnnounceTribeSymbolBattleStarted()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Started");

        var data = CreateData();
        if (!TryEnqueueForOtherShards(40, data))
            return;

        worldState.StartTribeSymbolBattle();
        BroadcastPrepared(40, data);

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

        logger.LogInformation("Tribe symbol battle opened; every tribe reset to its own symbol");
    }

    public void AnnounceTribeSymbolBattleEnded()
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolBattlePhase}", "Ended");

        var data = CreateData();
        if (!TryEnqueueForOtherShards(45, data))
            return;

        worldState.EndTribeSymbolBattle();
        BroadcastPrepared(45, data);

        logger.LogInformation("Tribe symbol battle closed");
    }

    public void AnnounceSymbolResolved(byte symbolIndex, byte winnerTribeId)
    {
        using var scope = logger.BeginScope("SymbolBattle {SymbolIndex} {WinnerTribeId}", symbolIndex, winnerTribeId);

        var data = CreateData(symbolIndex, winnerTribeId);
        if (!TryEnqueueForOtherShards(42, data))
            return;

        if (symbolIndex == WorldStateService.TribeCount)
            worldState.ResolveMonsterSymbol(winnerTribeId);
        else
            worldState.ResolveTribeSymbol(symbolIndex, winnerTribeId);

        BroadcastPrepared(42, data);

        logger.LogInformation("Symbol {SymbolIndex} resolved to tribe {WinnerTribeId}", symbolIndex, winnerTribeId);
    }

    public void AnnounceAllianceOffer(byte fromTribeId, byte toTribeId, bool isAccepted)
    {
        var data = CreateData(fromTribeId, toTribeId, isAccepted ? 1 : 0);
        if (!TryEnqueueForOtherShards(46, data))
            return;

        worldState.SetAllianceOffer(fromTribeId, toTribeId, isAccepted);
        BroadcastPrepared(46, data);
    }

    public void AnnounceAllianceDissolved(byte tribeA, byte tribeB)
    {
        var data = CreateData(tribeA, tribeB);
        if (!TryEnqueueForOtherShards(47, data))
            return;

        worldState.DissolveAlliance(tribeA, tribeB);
        BroadcastPrepared(47, data);
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

        if (!KnownTSortRegistry.IsKnown(TowerWarEventCodes.TowerState))
        {
            logger.LogError(
                "Tower status tSort {Sort} is absent from the known-sort allowlist; remote fan-out suppressed",
                TowerWarEventCodes.TowerState);
            return;
        }

        var snapshot = TowerStatusRelaySnapshot.FromResponse(in response);
        if (!TryEnqueueForOtherShards(TowerWarEventCodes.TowerState, snapshot.ToPayload()))
            return;

        BroadcastToEveryZone(in response);
    }

    public void AnnounceFfaCountdown(int minutesRemaining)
    {
        var data = CreateData(minutesRemaining);
        if (TryEnqueueForOtherShards(1501, data))
            BroadcastPrepared(1501, data);
    }

    public void AnnounceFfaGateOpen()
    {
        var data = CreateData();
        if (!TryEnqueueForOtherShards(1502, data))
            return;

        siegeState?.SetZone335(1);
        BroadcastPrepared(1502, data);
    }

    public void AnnounceFfaEntranceOpen()
    {
        var data = CreateData();
        if (!TryEnqueueForOtherShards(1503, data))
            return;

        siegeState?.SetZone335(2);
        BroadcastPrepared(1503, data);
    }

    public void AnnounceFfaBattleStart(int battleTimerLegacyTicks)
    {
        var data = CreateData(battleTimerLegacyTicks);
        if (!TryEnqueueForOtherShards(1504, data))
            return;

        siegeState?.SetZone335(3);
        BroadcastPrepared(1504, data);
    }

    public void AnnounceFfaBattleEnd()
    {
        var data = CreateData();
        if (!TryEnqueueForOtherShards(1505, data))
            return;

        siegeState?.SetZone335(4);
        BroadcastPrepared(1505, data);
    }

    public void AnnounceFfaClosedNotice()
    {
        var data = CreateData();
        if (!TryEnqueueForOtherShards(1506, data))
            return;

        siegeState?.SetZone335(5);
        BroadcastPrepared(1506, data);
    }

    public void AnnounceFfaReset()
    {
        var data = CreateData();
        if (!TryEnqueueForOtherShards(1507, data))
            return;

        siegeState?.ResetZone335();
        BroadcastPrepared(1507, data);
    }

    public bool AnnounceValleyWarGateCountdown(int remainingCount)
    {
        return BroadcastValleyWar(659, remainingCount);
    }

    public bool AnnounceValleyWarGateOpened()
    {
        return BroadcastValleyWar(660);
    }

    public bool AnnounceValleyWarGateClosed()
    {
        return BroadcastValleyWar(662);
    }

    public bool AnnounceValleyWarDoorOpened()
    {
        return BroadcastValleyWar(663);
    }

    public bool AnnounceValleyWarTribeWin(byte winningTribe)
    {
        return BroadcastValleyWar(666, winningTribe);
    }

    public bool AnnounceValleyWarBattleScrollDeleted()
    {
        return BroadcastValleyWar(667);
    }

    public bool AnnounceValleyWarBossDefeated()
    {
        return BroadcastValleyWar(668);
    }

    public bool AnnounceValleyWarReturnToTown()
    {
        return BroadcastValleyWar(669);
    }

    public ValleyWarEnvironmentSnapshot GetValleyWarEnvironmentSnapshot()
    {
        var eligiblePlayerPresent = false;
        var bossSlotOccupied = false;

        foreach (var zone in zones.Zones)
        {
            if (!ValleyWarMapCatalog.Contains(zone.MapId))
                continue;

            bossSlotOccupied |= zone.ValleyWarBossSlotOccupied();

            foreach (var player in zone.Players)
            {
                if (player.IsMovingZone || player.VisibleState == 0)
                    continue;

                eligiblePlayerPresent = true;
                break;
            }
        }

        return new ValleyWarEnvironmentSnapshot(eligiblePlayerPresent, bossSlotOccupied);
    }

    public bool HasCompleteValleyWarCampaignOwnership()
    {
        foreach (var mapId in ValleyWarMapCatalog.ConfiguredMaps)
            if (!zones.TryGet(mapId, out _))
                return false;

        return true;
    }

    public void AnnounceValleyWarCountdown(int contextTag, int value)
    {
        var response = new ZoneWar297StatusResponse { Value00 = contextTag, Value01 = contextTag, Value02 = value };
        BroadcastToValleyWarCampaign(in response);
    }

    public void AnnounceValleyWarKillRaceQuotas(ImmutableArray<int> quotas)
    {
        if (quotas.Length != ValleyWarSchedule.TribeCount)
            throw new ArgumentException($"Expected exactly {ValleyWarSchedule.TribeCount} quotas.", nameof(quotas));

        var response = new ZoneWar297MonsterCountResponse { MonsterNum = [.. quotas] };
        BroadcastToValleyWarCampaign(in response);
    }

    public void GrantValleyWarRewardsToTribe(byte winningTribe)
    {
        foreach (var zone in zones.Zones)
        {
            if (!ValleyWarMapCatalog.Contains(zone.MapId))
                continue;

            foreach (var player in zone.Players)
            {
                if (player.Tribe != winningTribe || player.IsMovingZone || player.IsDead)
                    continue;

                if (!zone.Post(ZoneCommand.GrantValleyWarRewardDrop(player.CharacterId)))
                    logger.LogWarning(
                        "Zone {MapId} inbox full: dropped valley-war reward drop grant for character {CharacterId}",
                        zone.MapId, player.CharacterId);
            }
        }
    }

    public void DisconnectValleyWarCampaign()
    {
        foreach (var zone in zones.Zones)
        {
            if (!ValleyWarMapCatalog.Contains(zone.MapId))
                continue;

            foreach (var player in zone.Players)
                if (player.Session is { } client)
                    client.Abort(DisconnectReason.ValleyWarForcedReset);
        }
    }

    private byte[] Broadcast(int sort, params ReadOnlySpan<int> fields)
    {
        var data = CreateData(fields);
        BroadcastPrepared(sort, data);

        return data;
    }

    private bool BroadcastValleyWar(int sort, params ReadOnlySpan<int> fields)
    {
        var data = CreateData(fields);
        if (!TryEnqueueForOtherShards(sort, data))
            return false;

        BroadcastValleyWarPrepared(sort, data);
        return true;
    }

    private byte[] BroadcastWithName(int sort, int value, string characterName)
    {
        var data = CreateDataWithName(value, characterName);
        BroadcastPrepared(sort, data);
        return data;
    }

    private static byte[] CreateData(params ReadOnlySpan<int> fields)
    {
        var data = new byte[DataSize];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * sizeof(int)), fields[i]);

        return data;
    }

    private static byte[] CreateDataWithName(int value, string characterName)
    {
        var data = CreateData(value);

        LegacyWireCodec.WriteFixedString(data.AsSpan(4, NameFieldSize), characterName);

        return data;
    }

    private void BroadcastPrepared(int sort, byte[] data)
    {
        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };
        BroadcastToEveryZone(in response);
    }

    private void BroadcastValleyWarPrepared(int sort, byte[] data)
    {
        var response = new ZoneEventInfoResponse { Sort = sort, Data = data };
        BroadcastToValleyWarCampaign(in response);
    }

    private bool TryEnqueueForOtherShards(int sort, ReadOnlySpan<byte> data)
    {
        if (!KnownTSortRegistry.CrossesShardBoundary(sort))
            return true;

        if (uplink is null)
        {
            logger.LogError(
                "RvR event sort {Sort} rejected before local state mutation or fan-out: durable relay services are unavailable",
                sort);
            return false;
        }

        var identity = WorldEventPublicationIdentity.Create();
        WorldEventUplinkResult result;
        try
        {
            result = uplink.Publish(sort, data, identity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "RvR event sort {Sort} operation {OperationId} faulted before local state mutation or fan-out",
                sort, identity.OperationId);
            return false;
        }

        if (result.IsEnqueued)
            return true;

        logger.LogWarning(
            "RvR event sort {Sort} operation {OperationId} rejected before local state mutation or fan-out: durable relay returned {Result}",
            sort, result.Identity.OperationId, result.Kind);
        return false;
    }

    public void ApplyRelayedStateAndReactions(int sort, ReadOnlySpan<byte> data)
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
            case 49:
                worldState.DissolveAlliance((byte)ReadInt32(data, 0), (byte)ReadInt32(data, 4));
                break;
        }

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
        BroadcastToZones(in response, false);
    }

    private void BroadcastToValleyWarCampaign<TPacket>(in TPacket response) where TPacket : struct, IOutgoingPacket
    {
        BroadcastToZones(in response, true);
    }

    private void BroadcastToZones<TPacket>(in TPacket response, bool valleyWarOnly)
        where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in response, span);

            foreach (var zone in zones.Zones)
            {
                if (valleyWarOnly && !ValleyWarMapCatalog.Contains(zone.MapId))
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
                            "Cluster-wide RvR broadcast to character {RecipientId} (zone {MapId}) failed",
                            player.CharacterId, zone.MapId);
                    }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
