using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Game;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatching;
using Fenrir.Network.RateLimiting;
using Fenrir.Network.Sessions;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Options;

namespace Fenrir.GameServer;

/// <summary>Owns the zone listen socket; the GameServer-side twin of <c>Fenrir.LoginServer.LoginConnectionHost</c>.</summary>
public sealed class ZoneConnectionHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zones,
    IFrameDispatcher dispatcher,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    IWriteBehindFlusher writeBehindFlusher,
    ILogger<ZoneConnectionHost> logger) : BackgroundService
{
    private FenrirTcpListener? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _listener = new FenrirTcpListener(
            new IPEndPoint(IPAddress.Any, opts.Port),
            static (sessionId, transport, remoteEndPoint) =>
                new ZoneClientSession(sessionId, transport, remoteEndPoint));

        logger.LogInformation("GameServer listening on port {Port} (shard {ShardId}, maps [{Maps}])", opts.Port,
            opts.ShardId, string.Join(", ", zones.Zones.Select(z => z.MapId).Order()));

        try
        {
            await _listener.AcceptLoopAsync(OnAcceptedAsync, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task OnAcceptedAsync(ClientSession session, SocketConnection connection, CancellationToken ct)
    {
        var zoneSession = (ZoneClientSession)session;
        registry.Register(zoneSession);

        try
        {
            Greet(zoneSession, connection);

            await Task.WhenAll(
                connection.RunIOAsync(ct),
                SessionLoop.RunAsync(zoneSession, dispatcher, rateLimiter, ct)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Zone session {SessionId} ended", zoneSession.SessionId);
        }
        finally
        {
            // A session that never completed world registration has no CurrentZone -- nothing to remove, nothing to flush.
            if (zoneSession is { CharacterId: { } characterId, CurrentZone: Zone zone })
            {
                // A dropped Leave (full inbox) can't be retried here, and would otherwise leave a permanent phantom
                // in the zone's player list -- log loudly rather than silently discard.
                if (!zone.Post(ZoneCommand.Leave(characterId)))
                    logger.LogError(
                        "Zone {MapId} inbox full: dropped Leave for character {CharacterId} on disconnect -- character remains a phantom in the zone until its next Move/handoff",
                        zone.MapId, characterId);

                // Immediate flush: the periodic 5 s/512-entity flush would otherwise leave this player's last position unpersisted.
                writeBehindFlusher.RequestImmediateFlush();
            }

            registry.Unregister(zoneSession.SessionId);
            rateLimiter.Remove(zoneSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Unlike Login's op 0, <c>ZC_CONNECT_OK_RECV</c> carries no packet-level XOR, only the stream cipher it seeds.</summary>
    private static void Greet(ZoneClientSession session, SocketConnection connection)
    {
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new ZoneGreetingResponse { RandomNumber = randomNumber });
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
