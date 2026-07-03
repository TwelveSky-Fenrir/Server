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

/// <summary>
///     Owns the zone listen socket for the process lifetime — the GameServer-side twin of
///     <c>Fenrir.LoginServer.LoginConnectionHost</c> (Phase 5). Binds <see cref="FenrirTcpListener" /> to
///     <see cref="GameServerOptions.Port" />, greets each accepted connection (<c>ZC_CONNECT_OK_RECV</c>, wire
///     contract §5.1) and seeds the inbound stream cipher (§3.4) before the connection's I/O pump and
///     <see cref="Fenrir.Network.Dispatching.SessionLoop" /> start.
/// </summary>
public sealed class ZoneConnectionHost(
    IOptions<GameServerOptions> options,
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
            opts.ShardId, string.Join(", ", opts.Maps));

        try
        {
            await _listener.AcceptLoopAsync(OnAcceptedAsync, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path.
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
            // A session that never completed world registration has no CurrentZone (and possibly no
            // CharacterId) -- nothing to remove, nothing to flush. The session's own zone reference is the
            // authoritative "where does this character live" (kept fresh across handoffs, ADR-0012).
            if (zoneSession is { CharacterId: { } characterId, CurrentZone: Zone zone })
            {
                // The connection is being torn down either way, so a dropped Leave (full inbox) cannot be
                // retried here -- but it WOULD otherwise leave the character as a permanent phantom in the
                // zone's _players (never removed from the AOI grid, still counted by CCU/write-behind), so
                // this is logged loudly rather than silently discarded.
                if (!zone.Post(ZoneCommand.Leave(characterId)))
                    logger.LogError(
                        "Zone {MapId} inbox full: dropped Leave for character {CharacterId} on disconnect -- character remains a phantom in the zone until its next Move/handoff",
                        zone.MapId, characterId);

                // Targeted, immediate flush on disconnect (architecture reference §10.5) -- the periodic
                // 5 s/512-entity flush would otherwise leave up to ~5 s of this player's last position unpersisted.
                writeBehindFlusher.RequestImmediateFlush();
            }

            registry.Unregister(zoneSession.SessionId);
            rateLimiter.Remove(zoneSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Op 0 greeting: unlike Login's op 0, <c>ZC_CONNECT_OK_RECV</c> carries NO packet-level XOR (wire
    ///     contract §3.6: "aucune (clair) ; porte la graine flux") — only the stream cipher it seeds is XORed
    ///     obfuscation, and only on the way IN. Seeded BEFORE the I/O pump starts, same ordering reason as Login.
    /// </summary>
    private static void Greet(ZoneClientSession session, SocketConnection connection)
    {
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new ZcConnectOkRecv { RandomNumber = randomNumber });
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
