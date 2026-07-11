using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    public const int MaxProxyShopSlots = 500;

    private readonly ConcurrentQueue<int> _pendingProxyShopCloses = new();

    private readonly List<int> _proxyShopNeighborScratch = [];

    private readonly ConcurrentDictionary<int, ProxyShopBroadcastEntry> _proxyShops = new();

    public int ProxyShopCount => _proxyShops.Count;

    public void RegisterProxyShop(ProxyShopBroadcastEntry entry)
    {
        entry.LastBroadcastAt = _clock;
        _proxyShops[entry.CharacterId] = entry;
    }

    public void RemoveProxyShop(int characterId)
    {
        _proxyShops.TryRemove(characterId, out _);
    }

    public bool TryUpdateProxyShopExpiration(int characterId, int newShopDate)
    {
        if (!_proxyShops.TryGetValue(characterId, out var entry))
            return false;

        entry.ShopDate = newShopDate;
        return true;
    }

    public IReadOnlyList<int> DrainPendingProxyShopCloses()
    {
        if (_pendingProxyShopCloses.IsEmpty)
            return [];

        List<int>? closed = null;
        while (_pendingProxyShopCloses.TryDequeue(out var characterId))
            (closed ??= []).Add(characterId);

        return (IReadOnlyList<int>?)closed ?? [];
    }

    private void RebroadcastProxyShops()
    {
        if (MapId != ProxyShopZonePolicy.ZoneNumber || _proxyShops.IsEmpty)
            return;

        var today = GameDate.Today();

        foreach (var (characterId, entry) in _proxyShops)
        {
            if (entry.ShopDate < today)
            {
                if (_proxyShops.TryRemove(characterId, out _))
                {
                    BroadcastProxyShopState(entry, 3);
                    _pendingProxyShopCloses.Enqueue(characterId);
                }

                continue;
            }

            if (_clock - entry.LastBroadcastAt < SimulationClock.ProxyShopRebroadcastInterval)
                continue;

            entry.LastBroadcastAt = _clock;
            BroadcastProxyShopState(entry, 0);
        }
    }

    private void BroadcastProxyShopState(ProxyShopBroadcastEntry entry, int checkChangeActionState)
    {
        var cell = _grid.CellOf(entry.PosX, entry.PosZ);
        if (!_grid.HasAnyNeighbor(cell))
            return;

        _proxyShopNeighborScratch.Clear();
        _grid.Neighbors(_proxyShopNeighborScratch, cell, entry.PosX, entry.PosY, entry.PosZ);
        var packet = new ProxyShopStallStateResponse
        {
            ServerIndex = entry.CharacterId,
            UniqueNumber = entry.UniqueNumber,
            ProxyObject = new ProxyStateInfo
            {
                Location = [entry.PosX, entry.PosY, entry.PosZ],
                Name = entry.OwnerName,
                PshopName = entry.ShopName
            },
            CheckChangeActionState = checkChangeActionState
        };

        var total = FrameWriter.FrameSizeOf<ProxyShopStallStateResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in _proxyShopNeighborScratch)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        !IsReviveHackBroadcastSuppressed(recipient))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} proxy-shop broadcast to character {RecipientId} failed",
                        MapId, id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
