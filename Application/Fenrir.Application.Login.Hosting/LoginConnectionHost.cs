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
    IGameServerDirectoryRepository directory,
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

    private FenrirTcpListener<LoginClientSession>? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _listener = new FenrirTcpListener<LoginClientSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            static (sessionId, transport, remoteEndPoint) =>
                new LoginClientSession(sessionId, transport, remoteEndPoint));

        logger.LogInformation("LoginServer listening on port {Port}", opts.Port);

        try
        {
            await _listener.AcceptLoopAsync(OnAcceptedAsync, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
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
            // ordering) -- so this runs before GreetAsync, not after.
            if (remoteIp is not null &&
                !await ipFloodGuard.TryAcquireConnectionAsync(remoteIp, ct).ConfigureAwait(false))
            {
                logger.LogWarning(
                    "Login connection rejected: session {SessionId} from {RemoteIp} blocked by IP flood guard",
                    loginSession.SessionId, remoteIp);
                return; // IP just got persistently blocked and every session sharing it (this one included) aborted
            }

            await GreetAsync(loginSession, connection, ct).ConfigureAwait(false);

            await Task.WhenAll(
                connection.RunIOAsync(ct),
                SessionLoop.RunAsync(loginSession, dispatcher, rateLimiter, ipFloodGuard, ct, logger)
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
            // LoginServer's configured Information floor (Servers/Fenrir.LoginServer/appsettings.json's
            // Logging:LogLevel:Default) with zero trace anywhere -- log it loudly instead so an unhandled
            // fault is never silently indistinguishable from an ordinary disconnect.
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
                await TearDownAccountSessionAsync(accountId, loginSession.AccountSessionToken).ConfigureAwait(false);
                await LogLoginSessionEndedAsync(accountId, loginSession.State).ConfigureAwait(false);
            }

            registry.Unregister(loginSession.SessionId);
            rateLimiter.Remove(loginSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Best-effort cross-process cleanup of this account's <c>runtime.AccountSessions</c> row -- must never
    ///     throw out of a connection-teardown path. <see cref="IAccountSessionRepository.ClearIfOwnerAsync" />
    ///     idempotently no-ops if the row already moved on (e.g. a newer login already replaced it).
    /// </summary>
    private async ValueTask TearDownAccountSessionAsync(int accountId, Guid? sessionToken)
    {
        try
        {
            await accountSessions.MarkTearingDownAsync(accountId, CancellationToken.None).ConfigureAwait(false);
            await accountSessions
                .ClearIfOwnerAsync(accountId, AccountSessionServerKind.Login, null, sessionToken ?? default,
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
    private async Task GreetAsync(LoginClientSession session, SocketConnection connection, CancellationToken ct)
    {
        // Legacy rand_nor()%1001 * rand_nor()%1001 need not be reproduced byte-for-byte, only "look random".
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new LoginGreetingResponse
        {
            RandomNumber = randomNumber,
            MaxPlayerNum = options.Value.MaxPlayerNum,
            GagePlayerNum = 0,
            PresentPlayerNum = await ReadLivePlayerCountAsync(ct).ConfigureAwait(false)
        });
    }

    /// <summary>
    ///     Real CCU across every live shard (<c>runtime.GameServerDirectory</c>, the same directory
    ///     <c>ZoneTransferHandler</c>/<c>ShardPartitionGuard</c> already use for shard selection), not
    ///     <see cref="SessionRegistry.Count" /> -- that only counts sockets currently mid-login on this one
    ///     process, unrelated to how many players are actually in the world.
    ///     A directory read failure must not block the greet (the count is purely cosmetic), so it degrades to 0.
    ///     Public (was internal + InternalsVisibleTo when this type lived in the Fenrir.LoginServer executable
    ///     assembly): now that it lives in Fenrir.Application.Login.Hosting, Fenrir.LoginServer.Tests needs
    ///     ordinary cross-assembly access, and this method is still only ever called from production code via
    ///     <see cref="GreetAsync" /> above.
    /// </summary>
    public async ValueTask<int> ReadLivePlayerCountAsync(CancellationToken ct)
    {
        try
        {
            var shards = await directory.GetDirectoryAsync(ct).ConfigureAwait(false);

            var total = 0;
            foreach (var shard in shards)
                total += shard.Ccu;
            return total;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to read runtime.GameServerDirectory for the login greeting's player count; reporting 0");
            return 0;
        }
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
