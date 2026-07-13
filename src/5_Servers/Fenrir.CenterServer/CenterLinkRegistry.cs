using System.Collections.Concurrent;
using Fenrir.Cluster;
using Fenrir.Cluster.EventBus;
using Fenrir.Cluster.Wire.Packets;
using Fenrir.Cluster.WorldState;
using Fenrir.Core.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer;

/// <summary>
/// Registre des liens serveur-à-serveur <b>authentifiés</b> tenu par le CenterServer, et impl unique des quatre
/// seams de diffusion sortante déclarés par le module <c>Fenrir.Cluster</c> :
/// <list type="bullet">
/// <item><see cref="WorldState.ICenterLinkBroadcaster"/> (fan-out d'events monde op33, par charge brute) ;</item>
/// <item><see cref="Fenrir.Cluster.ICenterLinkBroadcaster"/> (fan-out d'un paquet sortant typé, ex. op57 party) ;</item>
/// <item><see cref="ICenterPeerRegistry"/> (balayage d'inactivité pour le <c>PeerLivenessHost</c>) ;</item>
/// <item><see cref="ICenterCloseProxyRelay"/> (unicast op35 close-proxy vers une zone).</item>
/// </list>
/// Le host y (dés)inscrit chaque <see cref="CenterLinkSession"/> à l'authentification/teardown et rafraîchit
/// l'horodatage d'activité à chaque trame reçue. Le fan-out n'exclut jamais l'émetteur (parité legacy) et isole
/// l'échec d'envoi d'un lien mort pour ne pas interrompre la diffusion aux autres.
/// </summary>
public sealed class CenterLinkRegistry(ILogger<CenterLinkRegistry> logger)
    : Fenrir.Cluster.WorldState.ICenterLinkBroadcaster, Fenrir.Cluster.ICenterLinkBroadcaster, ICenterPeerRegistry,
        ICenterCloseProxyRelay
{
    private readonly ConcurrentDictionary<long, PeerLink> _links = new();

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

    public ValueTask BroadcastWorldEventAsync(int sort, ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        // Charge de 130 octets, copiée UNE fois dans le paquet réutilisé pour toutes les zones.
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

    public ValueTask SendCloseProxyAsync(int zoneNumber, int userIndex, int characterIndex, int openUi,
        CancellationToken cancellationToken)
    {
        // Routage par numéro de zone : nécessite un hello d'enregistrement de zone (le lien ne porte pas encore
        // son numéro de serveur). Tant que ce hello n'existe pas, l'unicast est un no-op silencieux tracé
        // (parité legacy : « aucune zone connectée pour ce numéro » = no-op).
        logger.LogDebug(
            "Close-proxy unicast to zone {ZoneNumber} skipped: per-zone link routing awaits the zone-register hello " +
            "(user {UserIndex}, character {CharacterIndex}, openUi {OpenUi})",
            zoneNumber, userIndex, characterIndex, openUi);
        return ValueTask.CompletedTask;
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

    private const int WorldEventDataSize = 130;

    private sealed class PeerLink(CenterLinkSession session, long lastActivityUtcTicks)
    {
        public CenterLinkSession Session { get; } = session;
        public long LastActivityUtcTicks = lastActivityUtcTicks;
    }
}
