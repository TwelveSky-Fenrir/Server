using Fenrir.Cluster;
using Fenrir.Cluster.EventBus;
using Fenrir.Cluster.WorldState;
using Fenrir.Core.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Center;
using System.Collections.Concurrent;
using ICenterLinkBroadcaster = Fenrir.Cluster.Party.ICenterLinkBroadcaster;

namespace Fenrir.CenterServer;

public sealed class CenterLinkRegistry(ILogger<CenterLinkRegistry> logger)
    : Cluster.WorldState.ICenterLinkBroadcaster, ICenterLinkBroadcaster, ICenterPeerRegistry,
        ICenterCloseProxyRelay
{
    private const int WorldEventDataSize = 130;
    private readonly ConcurrentDictionary<long, PeerLink> _links = new();

    public ValueTask SendCloseProxyAsync(int zoneNumber, int userIndex, int characterIndex, int openUi,
        CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "Close-proxy unicast to zone {ZoneNumber} skipped: per-zone link routing awaits the zone-register hello " +
            "(user {UserIndex}, character {CharacterIndex}, openUi {OpenUi})",
            zoneNumber, userIndex, characterIndex, openUi);
        return ValueTask.CompletedTask;
    }

    public ValueTask BroadcastWorldEventAsync(int sort, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var payload = new byte[WorldEventDataSize];
        data.Span[..Math.Min(data.Length, WorldEventDataSize)].CopyTo(payload);
        var packet = new WorldEventOutbound { Sort = sort, Data = payload };
        BroadcastToZones(in packet);
        return ValueTask.CompletedTask;
    }

    public void BroadcastToZones<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        foreach (var link in _links.Values)
            TrySend(link.Session, in packet);
    }

    public int DisconnectIdlePeers(TimeSpan idleThreshold, DateTimeOffset now)
    {
        var cutoff = now.UtcTicks - idleThreshold.Ticks;
        var dropped = 0;

        foreach (var link in _links.Values)
        {
            if (Volatile.Read(ref link.LastActivityUtcTicks) > cutoff)
                continue;

            link.Session.Abort(DisconnectReason.IdleTimeout);
            dropped++;
        }

        return dropped;
    }

    internal void Register(CenterLinkSession session, DateTimeOffset now)
    {
        _links[session.SessionId] = new PeerLink(session, now.UtcTicks);
    }

    internal void Unregister(long sessionId)
    {
        _links.TryRemove(sessionId, out _);
    }

    internal void RefreshActivity(long sessionId, DateTimeOffset now)
    {
        if (_links.TryGetValue(sessionId, out var link))
            Volatile.Write(ref link.LastActivityUtcTicks, now.UtcTicks);
    }

    private void TrySend<TPacket>(CenterLinkSession session, in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        try
        {
            session.Send(in packet);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fan-out to S2S link {SessionId} failed; skipping this link", session.SessionId);
        }
    }

    private sealed class PeerLink(CenterLinkSession session, long lastActivityUtcTicks)
    {
        public long LastActivityUtcTicks = lastActivityUtcTicks;
        public CenterLinkSession Session { get; } = session;
    }
}
