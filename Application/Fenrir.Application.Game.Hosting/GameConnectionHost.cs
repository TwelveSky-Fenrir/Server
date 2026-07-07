using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Hosting.World;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting;

/// <summary>Owns the zone listen socket; the GameServer-side twin of <c>Fenrir.LoginServer.LoginConnectionHost</c>.</summary>
public sealed class GameConnectionHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zones,
    IFrameDispatcher dispatcher,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    ICharacterWriteBehindFlusher writeBehindFlusher,
    IAccountSessionRepository accountSessions,
    IEventLogRepository eventLog,
    IpFloodGuard ipFloodGuard,
    TribeQuotaRegistry tribeQuota,
    ILogger<GameConnectionHost> logger) : BackgroundService
{
    /// <summary>
    ///     game.EventLog.EventCode for a character leaving a live zone (Category=Session, "Logout") -- see
    ///     Fenrir.Application.Login.Services.Login.LoginService's LoginSucceededEventCode remarks for the full
    ///     four-code cross-reference within this category.
    /// </summary>
    private const short LogoutEventCode = 4;

    // Tracks every still-running OnAcceptedAsync invocation so StopAsync can await full connection teardown
    // (including FlushFinalCharacterStateAsync/RequestImmediateFlush/disconnect logging in its own finally
    // block) before this host's own StopAsync returns -- see StopAsync's own remarks for the parity gap this
    // closes.
    private readonly ConcurrentDictionary<Task, byte> _inFlightConnections = new();

    private FenrirTcpListener<ZoneClientSession>? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _listener = new FenrirTcpListener<ZoneClientSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            // Not `static` anymore: captures `logger` so every accepted session can emit its own Debug-level
            // packet-sent log through the same ILogger<GameConnectionHost> instance SessionLoop already logs
            // packet-received/violation events through -- one closure allocated here, once, at ExecuteAsync
            // startup (not per connection: the delegate itself is reused for every accepted socket), well
            // within the "closure allocated once and reused is fine" budget.
            (sessionId, transport, remoteEndPoint) =>
                new ZoneClientSession(sessionId, transport, remoteEndPoint, logger));

        logger.LogInformation("GameServer listening on port {Port} (shard {ShardId}, maps [{Maps}])", opts.Port,
            opts.ShardId, string.Join(", ", zones.Zones.Select(z => z.MapId).Order()));

        try
        {
            await _listener.AcceptLoopAsync(TrackInFlightAsync, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    ///     Waits for every connection accepted by <see cref="ExecuteAsync" /> to finish its own
    ///     <see cref="OnAcceptedAsync" /> teardown before this method returns -- the fix for the parity gap
    ///     against legacy's force-save-then-poll-until-drained shutdown sequence (<c>MyGame::Free</c>,
    ///     Server/ts25playuser/S07_MyGame01.cpp:317-372). Without this override, the default (non-overridden)
    ///     <see cref="BackgroundService.StopAsync" /> would return as soon as the accept loop itself exits,
    ///     while <c>FenrirTcpListener{TSession}</c>'s own per-connection dispatch
    ///     (Network/Fenrir.Network.Transport/FenrirTcpListener.cs:33-73) keeps every already-accepted
    ///     connection's lifetime running fully detached and unawaited. <c>PositionWriteBehindHost</c> is
    ///     registered after this host in <c>HostingServiceCollectionExtensions.AddGameHosting</c>, so its own
    ///     <c>StopAsync</c> (and the shared write-behind flusher's own final drain) only runs once this method
    ///     returns -- letting every in-flight disconnect's own flush have a chance to land before that flusher
    ///     is itself cancelled.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stops the accept loop first (no new connections dispatched after this): base.StopAsync cancels
        // ExecuteAsync's stoppingToken and awaits AcceptLoopAsync's own exit.
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        var outstanding = _inFlightConnections.Keys.ToArray();
        if (outstanding.Length == 0)
            return;

        try
        {
            // Bounded by the same shutdown-timeout token the Generic Host already gives every hosted
            // service's StopAsync -- a stuck connection can't hang shutdown indefinitely.
            await Task.WhenAll(outstanding).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Either the shutdown-timeout budget elapsed, or one of the outstanding connections' own
            // OperationCanceledException fell through uncaught (OnAcceptedAsync's own catch deliberately
            // excludes it -- see its own remarks). Either way, the remaining connections are abandoned
            // exactly like an ungraceful kill would abandon them -- no worse than before this fix.
            logger.LogWarning(
                "GameServer shutdown proceeding with zone connection teardown still in flight (of {Count} originally outstanding)",
                outstanding.Length);
        }
        catch (Exception ex)
        {
            // Every per-connection fault is already logged inside OnAcceptedAsync's own catch block; a
            // WhenAll surfacing one of them here must not prevent shutdown from proceeding.
            logger.LogWarning(ex, "One or more zone connections faulted while tearing down during shutdown");
        }
    }

    /// <summary>
    ///     Thin wrapper so <see cref="StopAsync" /> can await every still-in-flight connection: registers
    ///     <see cref="OnAcceptedAsync" />'s own returned <see cref="Task" /> before returning it back up to
    ///     <c>FenrirTcpListener{TSession}.RunAcceptedAsync</c>'s <c>await onAccepted(...)</c> -- synchronous up
    ///     to that point even though <see cref="OnAcceptedAsync" /> is itself async, so the registration always
    ///     happens-before <see cref="ExecuteAsync" />'s own accept loop can exit and race
    ///     <see cref="StopAsync" />'s later read of <see cref="_inFlightConnections" />.
    /// </summary>
    private Task TrackInFlightAsync(ZoneClientSession zoneSession, SocketConnection connection, CancellationToken ct)
    {
        var task = OnAcceptedAsync(zoneSession, connection, ct);

        _inFlightConnections[task] = 0;
        _ = task.ContinueWith(t => _inFlightConnections.TryRemove(t, out _), TaskScheduler.Default);

        return task;
    }

    private async Task OnAcceptedAsync(ZoneClientSession zoneSession, SocketConnection connection, CancellationToken ct)
    {
        registry.Register(zoneSession);

        // Captured once: RemoteEndPoint is fixed at accept time (SocketConnection's own remark), and both the
        // acquire and the matching release below must key on the exact same string.
        var remoteIp = zoneSession.RemoteEndPoint?.Address.ToString();

        try
        {
            // Trigger A (contract): the concurrent-connection gauge must already be incremented and read back
            // before any connect-acknowledgement is sent (Server/ts25zone/S03_MyUser.cpp:460-474's early-return
            // ordering) -- so this runs before Greet, not after.
            if (remoteIp is not null &&
                !await ipFloodGuard.TryAcquireConnectionAsync(remoteIp, ct).ConfigureAwait(false))
            {
                // IP just got persistently blocked and every session sharing it (this one included) aborted
                logger.LogWarning("Zone connection {SessionId} from {RemoteIp} rejected by IP flood guard",
                    zoneSession.SessionId, remoteIp);
                return;
            }

            logger.LogInformation("Zone connection {SessionId} accepted from {RemoteIp}", zoneSession.SessionId,
                remoteIp);

            Greet(zoneSession, connection);

            await Task.WhenAll(
                connection.RunIOAsync(ct),
                SessionLoop.RunAsync(zoneSession, dispatcher, rateLimiter, ipFloodGuard, ct, logger)
            ).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The only point in this whole per-connection chain that catches every exception type.
            // SessionLoop's own dispatch try/catch (Network/Fenrir.Network.Dispatch/SessionLoop.cs) already
            // turns an in-handler fault into a logged (LogError), reason-recorded Abort() before returning
            // cleanly, so anything still reaching here bypassed that guard entirely -- e.g. a fault
            // decoding a frame outside FrameDecoder's own ProtocolViolationException branch, or elsewhere
            // in SessionLoop.RunAsync's own read loop. Logging this at Debug would vanish below
            // GameServer's configured Information floor (Servers/Fenrir.GameServer/appsettings.json's
            // Logging:LogLevel:Default) with zero trace anywhere -- log it loudly instead so an unhandled
            // fault is never silently indistinguishable from an ordinary disconnect.
            logger.LogError(ex, "Zone session {SessionId} ended abnormally due to an unhandled exception",
                zoneSession.SessionId);
        }
        // OperationCanceledException falls through uncaught: it is an expected shutdown/external-abort
        // signal (matching SessionLoop.RunAsync's own posture), not a fault, and is swallowed without
        // logging by FenrirTcpListener.RunAcceptedAsync one level up -- the cleanup block below still
        // runs unconditionally either way, since `finally` executes regardless of how this try exits.
        finally
        {
            if (remoteIp is not null)
                ipFloodGuard.ReleaseConnection(remoteIp);

            // A session that never completed world registration has no CurrentZone -- nothing to remove, nothing to flush.
            if (zoneSession is { CharacterId: { } characterId, CurrentZone: Zone zone })
            {
                // Ordering is load-bearing: this synchronous, targeted flush reads the character's CURRENT
                // Position/Vitals/Progression state directly from the zone's live registry and persists it
                // BEFORE the Leave command below is even posted -- so it can never race the zone's own
                // tick-driven removal from that same registry (up to a 50 ms tick period, and the shared
                // periodic flusher can itself be blocked behind an already-in-flight SQL round trip well
                // beyond that). A signal-only RequestImmediateFlush here instead would leave whether this
                // player's final in-play delta gets persisted or silently dropped up to that race -- see
                // PositionWriteBehindHost.FlushCharacterNowAsync's own remarks for the full citation.
                await FlushFinalCharacterStateAsync(characterId).ConfigureAwait(false);

                // A dropped Leave (full inbox) can't be retried here, and would otherwise leave a permanent phantom
                // in the zone's player list -- log loudly rather than silently discard.
                if (!zone.Post(ZoneCommand.Leave(characterId)))
                    logger.LogError(
                        "Zone {MapId} inbox full: dropped Leave for character {CharacterId} on disconnect -- character remains a phantom in the zone until its next Move/handoff",
                        zone.MapId, characterId);

                // No longer this player's own correctness guarantee (the awaited flush above already durably
                // captured their final state) -- just a courtesy nudge so the shared periodic drain catches up
                // sooner on any OTHER character already dirty in this same batch.
                writeBehindFlusher.RequestImmediateFlush();

                await LogLogoutAsync(zoneSession.AccountId, characterId, zone.MapId).ConfigureAwait(false);

                logger.LogInformation(
                    "Zone session {SessionId} for character {CharacterId} left map {MapId}", zoneSession.SessionId,
                    characterId, zone.MapId);
            }

            if (zoneSession.AccountId is { } accountId)
                await TearDownAccountSessionAsync(accountId, zoneSession.AccountSessionToken).ConfigureAwait(false);

            // Unconditional and idempotent: frees this connection's counted tribe-quota slot regardless of how
            // far it got (never temp-registered at all, stalled before avatar-selection, or fully in-world) --
            // same "disconnect always clears the flag" posture as legacy's own S03_MyUser.cpp:340-411.
            tribeQuota.Release(zoneSession.SessionId);

            registry.Unregister(zoneSession.SessionId);
            rateLimiter.Remove(zoneSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Best-effort synchronous, targeted persist of this one character's final Position/Vitals/Progression
    ///     state -- must never throw out of a connection-teardown path, same posture as
    ///     <see cref="TearDownAccountSessionAsync" />/<see cref="LogLogoutAsync" /> below. A thrown exception here
    ///     (e.g. a transient SQL failure) falls back to the character's pre-existing dirty-tracker entry (if any)
    ///     being picked up by the shared periodic/immediate-signal drain later -- reopening the very race this
    ///     method exists to close, but only in this already-abnormal failure case, and strictly better than
    ///     letting the exception abort Leave-posting/tribe-quota-release/session-teardown/socket-disposal below.
    /// </summary>
    private async ValueTask FlushFinalCharacterStateAsync(int characterId)
    {
        try
        {
            await writeBehindFlusher.FlushCharacterNowAsync(characterId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to flush final Position/Vitals/Progression state for character {CharacterId} on disconnect -- falling back to the periodic write-behind drain",
                characterId);
        }
    }

    /// <summary>
    ///     Best-effort cross-process cleanup of this account's <c>runtime.AccountSessions</c> row -- must never
    ///     throw out of a connection-teardown path. Both <see cref="IAccountSessionRepository.MarkTearingDownAsync" />
    ///     and <see cref="IAccountSessionRepository.ClearIfOwnerAsync" /> are gated on this connection's own
    ///     (ServerKind, ShardId, SessionToken) ownership and idempotently no-op if the row already moved on (e.g. a
    ///     newer login already replaced it) -- neither call can affect a row this connection no longer owns.
    /// </summary>
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

    /// <summary>
    ///     Best-effort game.EventLog audit row for a character leaving a live zone (Category=Session, "Logout")
    ///     -- must never throw out of a connection-teardown path, same posture as
    ///     <see cref="TearDownAccountSessionAsync" /> above. Fired only when the session actually reached
    ///     <see cref="ZoneClientSession.CurrentZone" /> (see this method's caller: an authenticated ticket that
    ///     never got past zone registration has nothing meaningful to log here, same reasoning as the
    ///     Leave-command/immediate-flush guard right above it).
    /// </summary>
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
