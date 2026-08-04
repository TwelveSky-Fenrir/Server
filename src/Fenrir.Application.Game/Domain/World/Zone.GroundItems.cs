using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const int GroundItemSlotCapacity = 4000;

    private readonly ConcurrentQueue<GroundItemEntity> _claimedGroundItemDespawns = new();

    private readonly List<int> _groundItemBroadcastNeighborScratch = [];

    private readonly Dictionary<int, TimeSpan> _groundItemLastRebroadcast = new();

    private readonly Dictionary<int, uint> _groundItemReservations = [];

    private readonly ConcurrentDictionary<int, GroundItemEntity> _groundItems = new();

    private readonly object _groundItemSlotGate = new();

    private int _groundItemUniqueNumberSeed;

    public int GroundItemCount => _groundItems.Count;

    public bool SpawnGroundItem(int itemId, int quantity, float posX, float posY, float posZ, string master,
        string partyName, int dropSort, int? instanceId = null)
    {
        return SpawnGroundItem(new GroundItemSpawnPlan(itemId, quantity, 0, 0, 0, 0, 0, posX, posY, posZ, master,
            partyName, dropSort), instanceId);
    }

    public bool SpawnGroundItem(in GroundItemSpawnPlan plan, int? instanceId = null)
    {
        var uniqueNumber = unchecked((uint)Interlocked.Increment(ref _groundItemUniqueNumberSeed));
        GroundItemEntity? entity = null;

        lock (_groundItemSlotGate)
        {
            for (var index = 0; index < GroundItemSlotCapacity; index++)
            {
                if (_groundItems.ContainsKey(index))
                    continue;

                var candidate = new GroundItemEntity(index, uniqueNumber, plan.ItemId, plan.Quantity, plan.Value,
                    plan.SerialNumber, plan.PosX, plan.PosY, plan.PosZ, TruncateName(plan.Master),
                    TruncateName(plan.PartyName), plan.DropSort, _clock, plan.SocketGem1, plan.SocketGem2,
                    plan.SocketGem3, instanceId);

                if (!_groundItems.TryAdd(index, candidate))
                    continue;

                entity = candidate;
                break;
            }
        }

        if (entity is null)
            return false;

        BroadcastGroundItemAction(entity, 1);

        _groundItemLastRebroadcast[entity.ServerIndex] =
            _clock - SimulationClock.RebroadcastStaggerOffset(entity.ServerIndex,
                SimulationClock.GroundItemRebroadcastInterval);
        return true;
    }

    private static string TruncateName(string name)
    {
        return name.Length <= 13 ? name : name[..13];
    }

    public GroundItemClaimOutcome TryReserveGroundItem(int serverIndex, uint expectedUniqueNumber, string claimantName,
        string? claimantPartyName, float claimantX, float claimantY, float claimantZ, out GroundItemEntity? item,
        int? claimantInstanceId = null)
    {
        lock (_groundItemSlotGate)
        {
            if (!_groundItems.TryGetValue(serverIndex, out var snapshot) ||
                snapshot.UniqueNumber != expectedUniqueNumber || snapshot.IsExpired(_clock) ||
                _groundItemReservations.ContainsKey(serverIndex))
            {
                item = null;
                return GroundItemClaimOutcome.NotFound;
            }

            if (!IsVisibleAcrossDungeonInstance(snapshot.InstanceId, claimantInstanceId))
            {
                item = null;
                return GroundItemClaimOutcome.NotFound;
            }

            if (!snapshot.IsClaimableBy(claimantName, claimantPartyName, _clock))
            {
                item = null;
                return GroundItemClaimOutcome.NotOwned;
            }

            var dx = snapshot.PosX - claimantX;
            var dy = snapshot.PosY - claimantY;
            var dz = snapshot.PosZ - claimantZ;
            if (dx * dx + dy * dy + dz * dz >
                GroundItemPickupPolicy.MaxPickupDistance * GroundItemPickupPolicy.MaxPickupDistance)
            {
                item = null;
                return GroundItemClaimOutcome.TooFar;
            }

            _groundItemReservations.Add(serverIndex, snapshot.UniqueNumber);
            item = snapshot;
            return GroundItemClaimOutcome.Success;
        }
    }

    public void ReleaseGroundItemReservation(int serverIndex, uint uniqueNumber)
    {
        lock (_groundItemSlotGate)
        {
            if (_groundItemReservations.TryGetValue(serverIndex, out var reservedUniqueNumber) &&
                reservedUniqueNumber == uniqueNumber)
                _groundItemReservations.Remove(serverIndex);
        }
    }

    public bool TryFinalizeGroundItemReservation(int serverIndex, uint uniqueNumber)
    {
        GroundItemEntity item;

        lock (_groundItemSlotGate)
        {
            if (!_groundItemReservations.TryGetValue(serverIndex, out var reservedUniqueNumber) ||
                reservedUniqueNumber != uniqueNumber || !_groundItems.TryGetValue(serverIndex, out var snapshot) ||
                snapshot.UniqueNumber != uniqueNumber ||
                !((ICollection<KeyValuePair<int, GroundItemEntity>>)_groundItems).Remove(
                    new KeyValuePair<int, GroundItemEntity>(serverIndex, snapshot)))
                return false;

            _groundItemReservations.Remove(serverIndex);
            item = snapshot;
        }

        _claimedGroundItemDespawns.Enqueue(item);
        return true;
    }

    private void RebroadcastGroundItems()
    {
        foreach (var (index, item) in _groundItems)
        {
            var last = _groundItemLastRebroadcast.TryGetValue(index, out var t) ? t : TimeSpan.MinValue;
            if (_clock - last < SimulationClock.GroundItemRebroadcastInterval)
                continue;

            BroadcastGroundItemAction(item, 2);
        }
    }

    private void ExpireGroundItems()
    {
        List<(int Index, GroundItemEntity Item)>? expired = null;
        foreach (var (index, item) in _groundItems)
            if (item.IsExpired(_clock))
                (expired ??= []).Add((index, item));

        if (expired is null)
            return;

        foreach (var (index, item) in expired)
        {
            GroundItemEntity? expiredItem = null;

            lock (_groundItemSlotGate)
            {
                if (_groundItemReservations.ContainsKey(index) ||
                    !_groundItems.TryGetValue(index, out var current) || current.UniqueNumber != item.UniqueNumber ||
                    !((ICollection<KeyValuePair<int, GroundItemEntity>>)_groundItems).Remove(
                        new KeyValuePair<int, GroundItemEntity>(index, current)))
                    continue;

                _groundItemLastRebroadcast.Remove(index);
                expiredItem = current;
            }

            if (expiredItem is not null)
                BroadcastGroundItemAction(expiredItem, 3);
        }
    }

    private void DrainClaimedGroundItemDespawns(int maximum)
    {
        for (var processed = 0; processed < maximum && _claimedGroundItemDespawns.TryDequeue(out var item); processed++)
        {
            BroadcastGroundItemAction(item, 3);
            _groundItemLastRebroadcast.Remove(item.ServerIndex);
        }
    }

    private void BroadcastGroundItemAction(GroundItemEntity item, int checkChangeActionState)
    {
        _groundItemLastRebroadcast[item.ServerIndex] = _clock;

        var cell = _grid.CellOf(item.PosX, item.PosZ);
        if (!_grid.HasAnyNeighbor(cell))
            return;

        _groundItemBroadcastNeighborScratch.Clear();
        _grid.Neighbors(_groundItemBroadcastNeighborScratch, cell, item.PosX, item.PosY, item.PosZ);
        var packet = BuildItemActionRecv(item, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<GroundItemReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in _groundItemBroadcastNeighborScratch)
                try
                {
                    if (TryGetBroadcastRecipient(id, out var recipient, out var clientSession) &&
                        IsVisibleAcrossDungeonInstance(item.InstanceId, recipient.DungeonInstanceId))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} ground-item broadcast to character {RecipientId} failed",
                        MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private GroundItemReplicationResponse BuildItemActionRecv(GroundItemEntity item,
        int checkChangeActionState)
    {
        return new GroundItemReplicationResponse
        {
            ServerIndex = item.ServerIndex,
            UniqueNumber = item.UniqueNumber,
            Data = new ObjectForItem
            {
                Index = item.ItemId,
                Quantity = item.Quantity,
                Value = item.Value,
                SerialNumber = item.SerialNumber,
                Location = [item.PosX, item.PosY, item.PosZ],
                Master = item.Master,
                PartyName = item.PartyName,
                DropSort = item.DropSort,
                CreateTime = item.CreateTime,
                PresentTime = item.PresentTimeAt(_clock),
                CreateState = item.CreateStateAt(_clock),
                SocketGem = [item.SocketGem1, item.SocketGem2, item.SocketGem3]
            },
            CheckChangeActionState = checkChangeActionState
        };
    }
}
