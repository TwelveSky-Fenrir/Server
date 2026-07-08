using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

/// <summary>
///     Owns the login listen socket; for each accepted connection, greets the client, seeds the stream cipher, then
///     pumps I/O.
/// </summary>
public sealed class LoginConnectionHost(
    IOptions<LoginServerOptions> options,
    IFrameDispatcher dispatcher,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    LoginCapacityState capacity,
    IAccountSessionRepository accountSessions,
    IEventLogRepository eventLog,
    IpFloodGuard ipFloodGuard,
    ILogger<LoginConnectionHost> logger) : BackgroundService
{
    /// <summary>
    ///     game.EventLog.EventCode for a login-side session ending (Category=Session) -- see
    ///     Fenrir.Application.Login.Services.Login.LoginService's LoginSucceededEventCode remarks for the full
    ///     four-code cross-reference within this category.
    /// </summary>
    private const short LoginSessionEndedEventCode = 2;

    // Tracks every still-running OnAcceptedAsync invocation so StopAsync can await full connection teardown
    // (including TearDownAccountSessionAsync/LogLoginSessionEndedAsync in its own finally block) before this
    // host's own StopAsync returns -- see StopAsync's own remarks for the parity gap this closes. Mirrors
    // Fenrir.Application.Game.Hosting.GameConnectionHost's own _inFlightConnections field.
    private readonly ConcurrentDictionary<Task, byte> _inFlightConnections = new();

    private FenrirTcpListener<LoginClientSession>? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _listener = new FenrirTcpListener<LoginClientSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            // Not `static` anymore: captures `logger` so every accepted session can emit its own Debug-level
            // packet-sent log through the same ILogger<LoginConnectionHost> instance SessionLoop already logs
            // packet-received/violation events through -- one closure allocated here, once, at ExecuteAsync
            // startup (not per connection: the delegate itself is reused for every accepted socket), well
            // within the "closure allocated once and reused is fine" budget.
            (sessionId, transport, remoteEndPoint) =>
                new LoginClientSession(sessionId, transport, remoteEndPoint, logger));

        logger.LogInformation("LoginServer listening on port {Port}", opts.Port);

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
    ///     connection's lifetime running fully detached and unawaited -- letting the process exit while a
    ///     still-in-flight connection's own TearDownAccountSessionAsync/LogLoginSessionEndedAsync write is
    ///     still pending or hasn't even started. Exact port of
    ///     <c>Fenrir.Application.Game.Hosting.GameConnectionHost.StopAsync</c>, which already carries this fix.
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
                "LoginServer shutdown proceeding with login connection teardown still in flight (of {Count} originally outstanding)",
                outstanding.Length);
        }
        catch (Exception ex)
        {
            // Every per-connection fault is already logged inside OnAcceptedAsync's own catch block; a
            // WhenAll surfacing one of them here must not prevent shutdown from proceeding.
            logger.LogWarning(ex, "One or more login connections faulted while tearing down during shutdown");
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
    private Task TrackInFlightAsync(LoginClientSession loginSession, SocketConnection connection, CancellationToken ct)
    {
        var task = OnAcceptedAsync(loginSession, connection, ct);

        _inFlightConnections[task] = 0;
        _ = task.ContinueWith(t => _inFlightConnections.TryRemove(t, out _), TaskScheduler.Default);

        return task;
    }

    private async Task OnAcceptedAsync(LoginClientSession loginSession, SocketConnection connection,
        CancellationToken ct)
    {
        registry.Register(loginSession);

        // Captured once: RemoteEndPoint is fixed at accept time (SocketConnection's own remark), and both the
        // acquire and the matching release below must key on the exact same string.
        var remoteIp = loginSession.RemoteEndPoint?.Address.ToString();

        logger.LogInformation("Login connection accepted: session {SessionId} from {RemoteIp}",
            loginSession.SessionId, remoteIp);

        try
        {
            // Trigger A (contract): the concurrent-connection gauge must already be incremented and read back
            // before any connect-acknowledgement is sent (Server/ts25login/S03_MyUser.cpp:194-209's early-return
            // ordering) -- so this runs before Greet, not after.
            if (remoteIp is not null &&
                !await ipFloodGuard.TryAcquireConnectionAsync(remoteIp, ct).ConfigureAwait(false))
            {
                logger.LogWarning(
                    "Login connection rejected: session {SessionId} from {RemoteIp} blocked by IP flood guard",
                    loginSession.SessionId, remoteIp);
                return; // IP just got persistently blocked and every session sharing it (this one included) aborted
            }

            Greet(loginSession, connection);

            await SessionLoop.RunConnectionAsync(connection, loginSession, dispatcher, rateLimiter, ipFloodGuard, ct,
                logger).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The only point in this whole per-connection chain that catches every exception type.
            // SessionLoop's own dispatch try/catch (Network/Fenrir.Network.Dispatch/SessionLoop.cs) already
            // turns an in-handler fault into a logged (LogError), reason-recorded Abort() before returning
            // cleanly, so anything still reaching here bypassed that guard entirely -- most commonly
            // SocketConnection.ReceiveLoopAsync/SendLoopAsync's own captured fault propagating out of
            // SessionLoop.RunAsync's PipeReader.ReadAsync (System.IO.Pipelines rethrows a faulted writer's
            // exception to its reader) rather than a fault inside a packet handler. TransportFaultClassifier
            // separates the routine case -- the peer closed abruptly (app closed, crash, network drop,
            // NAT/firewall reset), which is not a server bug -- from a genuine unexpected fault: logging
            // both identically at Error made an ordinary disconnect indistinguishable from a real crash in
            // this exact log line. A genuine unhandled fault (anything else -- e.g. a bug decoding a frame
            // outside FrameDecoder's own ProtocolViolationException branch) still needs to stay loud:
            // logging it at Debug would vanish below LoginServer's configured Information floor
            // (Servers/Fenrir.LoginServer/appsettings.json's Logging:LogLevel:Default) with zero trace anywhere.
            if (TransportFaultClassifier.IsExpectedDisconnect(ex))
                logger.LogInformation(
                    "Login session {SessionId} disconnected ({ExceptionType}: {Message})", loginSession.SessionId,
                    ex.GetType().Name, ex.Message);
            else
                logger.LogError(ex, "Login session {SessionId} ended abnormally due to an unhandled exception",
                    loginSession.SessionId);
        }
        // OperationCanceledException falls through uncaught: it is an expected shutdown/external-abort
        // signal (matching SessionLoop.RunAsync's own posture), not a fault, and is swallowed without
        // logging by FenrirTcpListener.RunAcceptedAsync one level up -- the cleanup block below still
        // runs unconditionally either way, since `finally` executes regardless of how this try exits.
        finally
        {
            if (remoteIp is not null)
                ipFloodGuard.ReleaseConnection(remoteIp);

            if (loginSession.AccountId is { } accountId)
            {
                logger.LogInformation(
                    "Login session {SessionId} ended for account {AccountId} in state {State}",
                    loginSession.SessionId, accountId, loginSession.State);

                // Bug fix (found investigating EndToEndScenarioTests' zone-handshake "peer closed after 0 of 5
                // bytes" failure): a session that reached HandoverIssued closed this Login connection ON
                // PURPOSE, because it already received its zone-transfer ticket and is expected to reconnect to
                // a GameServer shard next (ZoneTransferHandler's own remarks) -- that reconnect is GUARANTEED to
                // happen strictly after this Login socket closes, never before, since the same physical client
                // can't hold both legs open at once. At the moment this finally block runs, runtime.AccountSessions
                // still has ServerKind=Login (ZoneHandshakeService.ConsumeTicketAsync's own
                // usp_AccountSession_TransitionToGame call is what flips it to Game, and that hasn't run yet) --
                // so TearDownAccountSessionAsync's ownership match (ServerKind=Login, SessionToken=the same
                // token the ticket carries) ALWAYS matches and ALWAYS clears the row before the GameServer's own
                // handshake ever gets a chance to claim it, unconditionally turning every successful zone
                // transfer into a spurious ZoneHandshakeOutcome.SessionSuperseded silent disconnect. This is not
                // a rare interleaving to guard against defensively -- it is the deterministic, 100%-reproducible
                // ordering of the designed handoff, confirmed by re-running EndToEndScenarioTests in full
                // isolation (no other process touching the same database) and observing the identical failure
                // every time. Skip the teardown call for exactly this one terminal state; every earlier state
                // (an abandoned/failed login, or a session that disconnected before ever reaching char-select)
                // still tears its row down exactly as before -- usp_AccountSession_MarkTearingDown's own remarks
                // already anticipated this exact "Login-teardown-first" race as a way to "wrongly reject a
                // legitimate player as SessionSuperseded" but the ownership-match SQL alone can't distinguish an
                // intentional handoff from an abandoned session; only the caller (this class) has that context.
                if (loginSession.State != LoginSessionState.HandoverIssued)
                    await TearDownAccountSessionAsync(accountId, loginSession.AccountSessionToken)
                        .ConfigureAwait(false);

                await LogLoginSessionEndedAsync(accountId, loginSession.State).ConfigureAwait(false);
            }

            registry.Unregister(loginSession.SessionId);
            rateLimiter.Remove(loginSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Best-effort cross-process cleanup of this account's <c>runtime.AccountSessions</c> row -- must never
    ///     throw out of a connection-teardown path. Both <see cref="IAccountSessionRepository.MarkTearingDownAsync" />
    ///     and <see cref="IAccountSessionRepository.ClearIfOwnerAsync" /> are gated on this connection's own
    ///     (ServerKind, ShardId, SessionToken) ownership and idempotently no-op if the row already moved on (e.g. a
    ///     concurrent Game-side world-entry claim for the same account already reassigned it) -- neither call can
    ///     affect a row this connection no longer owns.
    /// </summary>
    private async ValueTask TearDownAccountSessionAsync(int accountId, Guid? sessionToken)
    {
        try
        {
            var resolvedToken = sessionToken ?? default;
            await accountSessions
                .MarkTearingDownAsync(accountId, AccountSessionServerKind.Login, null, resolvedToken,
                    CancellationToken.None)
                .ConfigureAwait(false);
            await accountSessions
                .ClearIfOwnerAsync(accountId, AccountSessionServerKind.Login, null, resolvedToken,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to tear down runtime.AccountSessions row for account {AccountId}",
                accountId);
        }
    }

    /// <summary>
    ///     Best-effort game.EventLog audit row for this login-side session ending -- must never throw out of a
    ///     connection-teardown path, same posture as <see cref="TearDownAccountSessionAsync" /> above. Outcome=1
    ///     when the session reached <see cref="LoginSessionState.HandoverIssued" /> (the normal path: the client
    ///     already received its zone-transfer ticket and is expected to reconnect to a GameServer shard next,
    ///     see <c>ZoneTransferHandler</c>) vs Outcome=0 for every earlier state (an authenticated session that
    ///     dropped before completing character selection/handoff).
    /// </summary>
    private async ValueTask LogLoginSessionEndedAsync(int accountId, LoginSessionState finalState)
    {
        try
        {
            var outcome = (byte)(finalState == LoginSessionState.HandoverIssued ? 1 : 0);
            await eventLog.LogAsync(LoginSessionEndedEventCode, EventLogCategory.Session, accountId, null, null,
                null, null, null, null, null, null, outcome, $"FinalState={finalState}", CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write game.EventLog row for login session end (account {AccountId})", accountId);
        }
    }

    /// <summary>Seeds the stream cipher key BEFORE the I/O pump starts, so no inbound byte is decoded with the wrong key.</summary>
    private void Greet(LoginClientSession session, SocketConnection connection)
    {
        // Legacy rand_nor()%1001 * rand_nor()%1001 need not be reproduced byte-for-byte, only "look random".
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        var packet = BuildGreetingPacket(randomNumber);
        session.Send(packet);

        // Deliberately does not log the XOR key/random number itself (same posture as PacketLog never logging
        // raw payload bytes -- a stream-cipher seed has no business in an operational log sink). Debug, not
        // Information: this fires once per accepted connection, right after the "connection accepted" line
        // above, so it would be pure noise at the default level -- useful only when actively diagnosing the
        // greet/cipher-seed step itself.
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Login session {SessionId}: greeted (max {MaxPlayerNum}, present {PresentPlayerNum})",
                session.SessionId, packet.MaxPlayerNum, packet.PresentPlayerNum);
    }

    /// <summary>
    ///     Builds the connect-time greeting packet straight from <see cref="LoginCapacityState" /> -- the exact
    ///     same live, ~1s-refreshed (<c>ServerQuotaRefreshHost</c>) snapshot <c>LoginCapacityGate.Evaluate</c>
    ///     reads on every login attempt (<c>LoginService.LoginAsync</c>). Both figures used to come from two
    ///     different, disconnected sources -- a static <c>Login:MaxPlayerNum</c> config value bound once at
    ///     process startup, and a separate CCU sum over <c>runtime.GameServerDirectory</c> -- so a client that
    ///     merely connected (before attempting to log in) could never see a live maintenance-mode toggle, and
    ///     the displayed population could diverge from the number the gate actually enforces. Reading
    ///     <see cref="LoginCapacityState" /> directly here closes both gaps: this is an in-memory
    ///     <c>Volatile.Read</c>, no I/O, so unlike the CCU sum it needs no failure-degrades-to-0 fallback.
    ///     Public (was internal + InternalsVisibleTo when this type lived in the Fenrir.LoginServer executable
    ///     assembly, and <c>ReadLivePlayerCountAsync</c> carried the same public-for-testability rationale before
    ///     this fix superseded it): Fenrir.LoginServer.Tests needs ordinary cross-assembly access to observe the
    ///     greeting's value-selection without standing up a real socket, and this method is still only ever
    ///     called from production code via <see cref="Greet" /> above.
    /// </summary>
    public LoginGreetingResponse BuildGreetingPacket(int randomNumber)
    {
        return new LoginGreetingResponse
        {
            RandomNumber = randomNumber,
            MaxPlayerNum = capacity.MaxPlayers,
            GagePlayerNum = 0,
            PresentPlayerNum = capacity.CurrentPlayers
        };
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
