using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Fenrir.Protocol.Game;
using Fenrir.Security.Abstractions;
using Fenrir.Security.FloodProtection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

public sealed class GameConnectionHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zones,
    IFrameDispatcher dispatcher,
    IOpcodeFrameSizeProvider opcodeRegistry,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    ICharacterWriteBehindFlusher writeBehindFlusher,
    IAccountSessionRepository accountSessions,
    IEventLogRepository eventLog,
    IpFloodGuard ipFloodGuard,
    TribeQuotaRegistry tribeQuota,
    ILogger<GameConnectionHost> logger) : BackgroundService
{
    private const short LogoutEventCode = 4;

    private readonly ConcurrentDictionary<Task, byte> _inFlightConnections = new();

    private readonly List<(short MapId, int Port, TcpServer<ZoneClientSession> Server)> _servers = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        var hostedMaps = zones.Zones.Select(z => z.MapId).Order().ToArray();

        var failedBinds = new List<(short MapId, int Port, SocketException Error)>();

        foreach (var mapId in hostedMaps)
        {
            var port = opts.ZoneBasePort + mapId;

            try
            {
                _servers.Add((mapId, port, new TcpServer<ZoneClientSession>(
                    new IPEndPoint(IPAddress.Any, port),
                    (sessionId, transport, remoteEndPoint) =>
                        new ZoneClientSession(sessionId, transport, remoteEndPoint, logger),
                    dispatcher,
                    opcodeRegistry,
                    rateLimiter,
                    ipFloodGuard,
                    logger)));

                logger.LogInformation("GameServer zone listener bound: map {MapId} on port {Port} (shard {ShardId})",
                    mapId, port, opts.ShardId);
            }
            catch (SocketException ex)
            {
                failedBinds.Add((mapId, port, ex));

                logger.LogError(ex,
                    "GameServer could not bind the zone listener for map {MapId} on port {Port} (shard {ShardId}): " +
                    "{SocketError}", mapId, port, opts.ShardId, ex.SocketErrorCode);
            }
        }

        var armedCount = _servers.Count;

        logger.LogInformation(
            "GameServer zone listeners: {ArmedCount} armed / {HostedCount} hosted, on ZoneBasePort {BasePort} + " +
            "mapId (shard {ShardId}); a client entering map N connects to {BasePort}+N. Maps [{Maps}]",
            armedCount, hostedMaps.Length, opts.ZoneBasePort, opts.ShardId, opts.ZoneBasePort,
            string.Join(", ", hostedMaps));

        if (failedBinds.Count > 0)
        {
            ReleaseArmedListeners();
            throw BuildIncompleteBindFailure(opts.ShardId, armedCount, hostedMaps.Length, failedBinds);
        }

        var acceptLoops = new List<Task>(_servers.Count);
        foreach (var entry in _servers)
        {
            var accepting = entry.Server;
            acceptLoops.Add(accepting.AcceptLoopAsync(
                (session, connection, ct) => TrackInFlightAsync(accepting, session, connection, ct), stoppingToken));
        }

        try
        {
            await Task.WhenAll(acceptLoops).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static InvalidOperationException BuildIncompleteBindFailure(byte shardId, int armedCount, int hostedCount,
        List<(short MapId, int Port, SocketException Error)> failedBinds)
    {
        var scope = armedCount == 0
            ? $"none of the {hostedCount} hosted zone listener(s) could be bound"
            : $"only {armedCount} of {hostedCount} zone listener(s) could be bound";

        var detail = string.Join("; ",
            failedBinds.Select(failure => $"map {failure.MapId} on port {failure.Port} ({failure.Error.SocketErrorCode})"));

        return new InvalidOperationException(
            $"Shard {shardId} cannot start: {scope} -- {detail}. LoginServer's zone transfer and ZoneMoveService " +
            "both hand clients ZoneBasePort + mapId computed from configuration, never from an observed listener, " +
            "and nothing anywhere retries a failed bind, so an unbound map is permanently unreachable (and ejects " +
            "any player transferring into it) while ZoneTickHost keeps simulating it. A busy port also means some " +
            "other process already owns this map, i.e. the same ADR-0012 disjointness violation ShardPartitionGuard " +
            "refuses at boot -- a partially armed shard is a split-brain risk, not degraded availability. Free the " +
            "port (usually a stale shard process) and restart the shard.",
            failedBinds[0].Error);
    }

    private void ReleaseArmedListeners()
    {
        foreach (var entry in _servers)
            entry.Server.DisposeAsync().AsTask().GetAwaiter().GetResult();

        _servers.Clear();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        var outstanding = _inFlightConnections.Keys.ToArray();
        if (outstanding.Length == 0)
            return;

        try
        {
            await Task.WhenAll(outstanding).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "GameServer shutdown proceeding with zone connection teardown still in flight (of {Count} originally outstanding)",
                outstanding.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "One or more zone connections faulted while tearing down during shutdown");
        }
    }

    private Task TrackInFlightAsync(TcpServer<ZoneClientSession> server, ZoneClientSession zoneSession,
        SocketConnection connection, CancellationToken ct)
    {
        var task = OnAcceptedAsync(server, zoneSession, connection, ct);

        _inFlightConnections[task] = 0;
        _ = task.ContinueWith(t => _inFlightConnections.TryRemove(t, out _), TaskScheduler.Default);

        return task;
    }

    private async Task OnAcceptedAsync(TcpServer<ZoneClientSession> server, ZoneClientSession zoneSession,
        SocketConnection connection, CancellationToken ct)
    {
        registry.Register(zoneSession);

        var remoteIp = zoneSession.RemoteEndPoint?.Address.ToString();

        try
        {
            if (remoteIp is not null &&
                !await ipFloodGuard.TryAcquireConnectionAsync(remoteIp, ct).ConfigureAwait(false))
            {
                logger.LogWarning("Zone connection {SessionId} from {RemoteIp} rejected by IP flood guard",
                    zoneSession.SessionId, remoteIp);
                return;
            }

            logger.LogInformation("Zone connection {SessionId} accepted from {RemoteIp}", zoneSession.SessionId,
                remoteIp);

            Greet(zoneSession, connection);

            await server.RunSessionAsync(connection, zoneSession, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (TransportFaultClassifier.IsExpectedDisconnect(ex))
                logger.LogInformation(
                    "Zone session {SessionId} disconnected ({ExceptionType}: {Message})", zoneSession.SessionId,
                    ex.GetType().Name, ex.Message);
            else
                logger.LogError(ex, "Zone session {SessionId} ended abnormally due to an unhandled exception",
                    zoneSession.SessionId);
        }
        finally
        {
            if (remoteIp is not null)
                ipFloodGuard.ReleaseConnection(remoteIp);

            if (zoneSession is { CharacterId: { } characterId, CurrentZone: Zone zone })
            {
                await FlushFinalCharacterStateAsync(characterId).ConfigureAwait(false);

                zone.TryGetPlayer(characterId, out var departingState);
                var wasMovingZone = departingState is not null && departingState.IsMovingZone;

                if (!zone.Post(ZoneCommand.Leave(characterId)))
                    logger.LogError(
                        "Zone {MapId} inbox full: dropped Leave for character {CharacterId} on disconnect -- character remains a phantom in the zone until its next Move/handoff",
                        zone.MapId, characterId);

                writeBehindFlusher.RequestImmediateFlush();

                if (!wasMovingZone)
                    await LogLogoutAsync(zoneSession.AccountId, characterId, zone.MapId).ConfigureAwait(false);

                logger.LogInformation(
                    "Zone session {SessionId} for character {CharacterId} left map {MapId}", zoneSession.SessionId,
                    characterId, zone.MapId);
            }

            if (zoneSession is { AccountId: { } accountId, IsZoneTransferPending: false })
                await TearDownAccountSessionAsync(accountId, zoneSession.AccountSessionToken).ConfigureAwait(false);

            tribeQuota.Release(zoneSession.SessionId);

            registry.Unregister(zoneSession.SessionId);
            rateLimiter.Remove(zoneSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask FlushFinalCharacterStateAsync(int characterId)
    {
        try
        {
            await writeBehindFlusher.FlushCharacterNowAsync(characterId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unexpected failure attempting the final Position/Vitals/Progression flush for character {CharacterId} on disconnect",
                characterId);
        }
    }

    private async ValueTask TearDownAccountSessionAsync(int accountId, Guid? sessionToken)
    {
        try
        {
            var resolvedToken = sessionToken ?? default;
            await accountSessions
                .MarkTearingDownAsync(accountId, AccountSessionServerKind.Game, options.Value.ShardId,
                    resolvedToken, CancellationToken.None)
                .ConfigureAwait(false);
            await accountSessions
                .ClearIfOwnerAsync(accountId, AccountSessionServerKind.Game, options.Value.ShardId,
                    resolvedToken, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to tear down runtime.AccountSessions row for account {AccountId}",
                accountId);
        }
    }

    private async ValueTask LogLogoutAsync(int? accountId, int characterId, short mapId)
    {
        try
        {
            await eventLog.LogAsync(LogoutEventCode, EventLogCategory.Session, accountId, characterId, null, null,
                options.Value.ShardId, null, null, null, null, 1, $"MapId={mapId}", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write game.EventLog row for logout (character {CharacterId})",
                characterId);
        }
    }

    private static void Greet(ZoneClientSession session, SocketConnection connection)
    {
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new ZoneGreetingResponse { RandomNumber = randomNumber });
    }

    public override void Dispose()
    {
        ReleaseArmedListeners();
        base.Dispose();
    }
}
