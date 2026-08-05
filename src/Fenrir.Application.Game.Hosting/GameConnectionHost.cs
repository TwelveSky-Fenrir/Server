using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.Sessions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Fenrir.Protocol.Game;
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
    GameConnectionReadiness readiness,
    ILogger<GameConnectionHost> logger) : BackgroundService
{
    private const short LogoutEventCode = 4;

    private const int GreetingRandomModulus = 1001;

    private const int SocketAdmissionHeadroomFactor = 2;

    private const int MaxProcessPendingOutboundFrames = 262_144;

    private const int MaxProcessPendingOutboundBytes = 256 * 1024 * 1024;

    private readonly SocketAdmissionGate _admissionGate = new(ResolveMaxConcurrentSockets(options.Value));

    private readonly ConcurrentDictionary<Task, byte> _deferredLeaveFinalizations = new();

    private readonly SemaphoreSlim _disconnectSqlGate = new(1, 1);

    private readonly ConcurrentDictionary<Task, byte> _inFlightConnections = new();

    private readonly OutboundBufferAdmissionGate _outboundAdmissionGate = new(MaxProcessPendingOutboundFrames,
        MaxProcessPendingOutboundBytes);

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
                        new ZoneClientSession(sessionId, transport, mapId, remoteEndPoint, logger,
                            _outboundAdmissionGate),
                    dispatcher,
                    opcodeRegistry,
                    rateLimiter,
                    ipFloodGuard,
                    logger,
                    applyOsSocketBuffers: true,
                    admissionGate: _admissionGate)));

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
            "mapId (shard {ShardId}); a client entering map N connects to {BasePort}+N. Maps [{Maps}]. " +
            "Socket admission is capped GameServer-process-wide at {MaxConcurrentSockets} concurrent socket(s) " +
            "(Game:Capacity {Capacity} x {HeadroomFactor}), shared by every listener above. Pending outbound " +
            "frames and bytes are also capped process-wide at {MaxPendingFrames} frames and {MaxPendingBytes} bytes",
            armedCount, hostedMaps.Length, opts.ZoneBasePort, opts.ShardId, opts.ZoneBasePort,
            string.Join(", ", hostedMaps), _admissionGate.MaxConcurrentSockets, opts.Capacity,
            SocketAdmissionHeadroomFactor, _outboundAdmissionGate.MaxPendingFrames,
            _outboundAdmissionGate.MaxPendingBytes);

        if (failedBinds.Count > 0)
        {
            ReleaseArmedListeners();
            var failure = BuildIncompleteBindFailure(opts.ShardId, armedCount, hostedMaps.Length, failedBinds);
            readiness.MarkFailed(failure);
            throw failure;
        }

        readiness.MarkReady();

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

    private static int ResolveMaxConcurrentSockets(GameServerOptions opts)
    {
        var cap = (long)Math.Max(1, opts.Capacity) * SocketAdmissionHeadroomFactor;
        return (int)Math.Min(cap, int.MaxValue);
    }

    private static InvalidOperationException BuildIncompleteBindFailure(byte shardId, int armedCount, int hostedCount,
        List<(short MapId, int Port, SocketException Error)> failedBinds)
    {
        var scope = armedCount == 0
            ? $"none of the {hostedCount} hosted zone listener(s) could be bound"
            : $"only {armedCount} of {hostedCount} zone listener(s) could be bound";

        var detail = string.Join("; ",
            failedBinds.Select(failure =>
                $"map {failure.MapId} on port {failure.Port} ({failure.Error.SocketErrorCode})"));

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

        var outstanding = _inFlightConnections.Keys.Concat(_deferredLeaveFinalizations.Keys).ToArray();
        if (outstanding.Length == 0)
            return;

        try
        {
            await Task.WhenAll(outstanding).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "GameServer shutdown proceeding with zone connection or deferred leave cleanup still in flight (of {Count} originally outstanding)",
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

            var completedHandoff = zoneSession.IsZoneTransferPending &&
                                   zoneSession.IsZoneTransferHandoffCommitted;

            if (zoneSession is { CharacterId: { } characterId, CurrentZone: Zone zone })
            {
                var leave =
                    await zone.PostLeaveCommandAndWaitAsync(characterId, zoneSession.SessionId,
                            CancellationToken.None)
                        .ConfigureAwait(false);

                var finalization = leave.PendingResult is { } pendingLeave
                    ? FinalizeDeferredLeaveAsync(pendingLeave, zoneSession, characterId, zone.MapId, completedHandoff)
                    : FinalizeLeaveAndReleaseSessionAsync(leave.Result, zoneSession, characterId, zone.MapId,
                        completedHandoff);

                if (finalization.IsCompleted)
                {
                    await finalization.ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning(
                        "Zone {MapId} leave for closed session {SessionId}, character {CharacterId} remains pending; its account lease will stay held until the actor decision and terminal snapshot are durable",
                        zone.MapId, zoneSession.SessionId, characterId);
                    TrackDeferredLeaveFinalization(finalization);
                }
            }
            else if (zoneSession is { AccountId: { } accountId } && !completedHandoff)
            {
                await TearDownAccountSessionAsync(accountId, zoneSession.AccountSessionToken).ConfigureAwait(false);
            }

            tribeQuota.Release(zoneSession);

            registry.Unregister(zoneSession.SessionId);
            rateLimiter.Remove(zoneSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task FinalizeDeferredLeaveAsync(Task<ZoneLeaveResult> pendingLeave, ZoneClientSession zoneSession,
        int characterId, short mapId, bool completedHandoff)
    {
        try
        {
            var result = await pendingLeave.ConfigureAwait(false);
            await FinalizeLeaveAndReleaseSessionAsync(result, zoneSession, characterId, mapId, completedHandoff)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Deferred zone leave finalization faulted for closed session {SessionId}, character {CharacterId} on map {MapId}",
                zoneSession.SessionId, characterId, mapId);
        }
    }

    private async Task FinalizeLeaveAndReleaseSessionAsync(ZoneLeaveResult leave, ZoneClientSession zoneSession,
        int characterId,
        short mapId, bool completedHandoff)
    {
        var requiresTerminalPersistence = leave.DepartingState is not null;

        if (requiresTerminalPersistence && !completedHandoff && zoneSession.AccountId is { } accountId)
            await MarkAccountSessionTearingDownAsync(accountId, zoneSession.AccountSessionToken).ConfigureAwait(false);

        await FinalizeLeaveAsync(leave, zoneSession, characterId, mapId, completedHandoff).ConfigureAwait(false);

        if (!completedHandoff && zoneSession.AccountId is { } releasableAccountId)
            await ClearAccountSessionAsync(releasableAccountId, zoneSession.AccountSessionToken).ConfigureAwait(false);
    }

    private async ValueTask FinalizeLeaveAsync(ZoneLeaveResult leave, ZoneClientSession zoneSession, int characterId,
        short mapId, bool completedHandoff)
    {
        if (leave.DepartingState is null)
        {
            logger.LogWarning(
                "Zone leave for closed session {SessionId}, character {CharacterId} on map {MapId} completed as {LeaveKind} ({Cause}); the actor did not remove this session, so no terminal snapshot flush is required",
                zoneSession.SessionId, characterId, mapId, leave.Kind, leave.Cause);
            return;
        }

        var persistence = await writeBehindFlusher.FlushCharacterSnapshotAsync(leave.DepartingState,
            CancellationToken.None).ConfigureAwait(false);

        if (!persistence.IsDurablyPersisted)
        {
            logger.LogWarning(
                "Final snapshot for character {CharacterId} is retained for retry; account-session release waits for durable completion",
                characterId);
            await persistence.Completion.ConfigureAwait(false);
        }

        writeBehindFlusher.RequestImmediateFlush();
        if (!completedHandoff)
            await LogLogoutAsync(zoneSession.AccountId, characterId, mapId).ConfigureAwait(false);

        logger.LogInformation(
            "Zone session {SessionId} for character {CharacterId} leave on map {MapId} completed as {LeaveKind}",
            zoneSession.SessionId, characterId, mapId, leave.Kind);
    }

    private void TrackDeferredLeaveFinalization(Task finalization)
    {
        _deferredLeaveFinalizations[finalization] = 0;
        _ = finalization.ContinueWith(task => _deferredLeaveFinalizations.TryRemove(task, out _),
            TaskScheduler.Default);
    }

    private async ValueTask TearDownAccountSessionAsync(int accountId, Guid? sessionToken)
    {
        await MarkAccountSessionTearingDownAsync(accountId, sessionToken).ConfigureAwait(false);
        await ClearAccountSessionAsync(accountId, sessionToken).ConfigureAwait(false);
    }

    private async ValueTask MarkAccountSessionTearingDownAsync(int accountId, Guid? sessionToken)
    {
        await _disconnectSqlGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var resolvedToken = sessionToken ?? default;
            await accountSessions
                .MarkTearingDownAsync(accountId, AccountSessionServerKind.Game, options.Value.ShardId,
                    resolvedToken, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to mark runtime.AccountSessions row as tearing down for account {AccountId}",
                accountId);
        }
        finally
        {
            _disconnectSqlGate.Release();
        }
    }

    private async ValueTask ClearAccountSessionAsync(int accountId, Guid? sessionToken)
    {
        await _disconnectSqlGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            var resolvedToken = sessionToken ?? default;
            await accountSessions
                .ClearIfOwnerAsync(accountId, AccountSessionServerKind.Game, options.Value.ShardId,
                    resolvedToken, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to clear runtime.AccountSessions row for account {AccountId}", accountId);
        }
        finally
        {
            _disconnectSqlGate.Release();
        }
    }

    private async ValueTask LogLogoutAsync(int? accountId, int characterId, short mapId)
    {
        await _disconnectSqlGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
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
        finally
        {
            _disconnectSqlGate.Release();
        }
    }

    private static void Greet(ZoneClientSession session, SocketConnection connection)
    {
        var randomNumber = GenerateGreetingRandomNumber();

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new ZoneGreetingResponse { RandomNumber = randomNumber });
    }

    private static int GenerateGreetingRandomNumber()
    {
        return RandomNumberGenerator.GetInt32(GreetingRandomModulus) *
               RandomNumberGenerator.GetInt32(GreetingRandomModulus);
    }

    public override void Dispose()
    {
        ReleaseArmedListeners();
        _disconnectSqlGate.Dispose();
        base.Dispose();
    }
}
