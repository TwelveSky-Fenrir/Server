using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Login.Sessions;
using Fenrir.Domain.Login;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Transport;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Hosting;

public sealed class LoginConnectionHost(
    IOptions<LoginServerOptions> options,
    IFrameDispatcher dispatcher,
    IOpcodeFrameSizeProvider opcodeRegistry,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    LoginIdleClock idleClock,
    LoginCapacityState capacity,
    IAccountSessionRepository accountSessions,
    IEventLogRepository eventLog,
    IpFloodGuard ipFloodGuard,
    LoginSocketAdmissionGate admissionGate,
    ILogger<LoginConnectionHost> logger) : BackgroundService
{
    private const short LoginSessionEndedEventCode = 2;

    private const int GreetingRandomModulus = 1001;

    private readonly ConcurrentDictionary<Task, byte> _inFlightConnections = new();

    private TcpServer<LoginClientSession>? _server;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _server = new TcpServer<LoginClientSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            (sessionId, transport, remoteEndPoint) =>
                new LoginClientSession(sessionId, transport, remoteEndPoint, logger),
            dispatcher,
            opcodeRegistry,
            rateLimiter,
            ipFloodGuard,
            logger);

        logger.LogInformation("LoginServer listening on port {Port}", opts.Port);

        try
        {
            await _server.AcceptLoopAsync(TrackInFlightAsync, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
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
                "LoginServer shutdown proceeding with login connection teardown still in flight (of {Count} originally outstanding)",
                outstanding.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "One or more login connections faulted while tearing down during shutdown");
        }
    }

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
        var remoteIp = loginSession.RemoteEndPoint?.Address.ToString();

        if (!admissionGate.TryAcquire())
        {
            logger.LogWarning(
                "Login connection refused: session {SessionId} from {RemoteIp} -- the server already holds its {MaxConcurrentConnections} concurrent sockets",
                loginSession.SessionId, remoteIp, admissionGate.MaxConcurrentConnections);

            await connection.DisposeAsync().ConfigureAwait(false);
            return;
        }

        registry.Register(loginSession);
        idleClock.Arm(loginSession, DateTimeOffset.UtcNow);

        logger.LogInformation("Login connection accepted: session {SessionId} from {RemoteIp}",
            loginSession.SessionId, remoteIp);

        try
        {
            if (remoteIp is not null &&
                !await ipFloodGuard.TryAcquireConnectionAsync(remoteIp, ct).ConfigureAwait(false))
            {
                logger.LogWarning(
                    "Login connection rejected: session {SessionId} from {RemoteIp} blocked by IP flood guard",
                    loginSession.SessionId, remoteIp);
                return;
            }

            Greet(loginSession, connection);

            await _server!.RunSessionAsync(connection, loginSession, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (TransportFaultClassifier.IsExpectedDisconnect(ex))
                logger.LogInformation(
                    "Login session {SessionId} disconnected ({ExceptionType}: {Message})", loginSession.SessionId,
                    ex.GetType().Name, ex.Message);
            else
                logger.LogError(ex, "Login session {SessionId} ended abnormally due to an unhandled exception",
                    loginSession.SessionId);
        }
        finally
        {
            if (remoteIp is not null)
                ipFloodGuard.ReleaseConnection(remoteIp);

            if (loginSession.AccountId is { } accountId)
            {
                logger.LogInformation(
                    "Login session {SessionId} ended for account {AccountId} in state {State}",
                    loginSession.SessionId, accountId, loginSession.State);

                if (loginSession.State != LoginSessionState.HandoverIssued)
                    await TearDownAccountSessionAsync(accountId, loginSession.AccountSessionToken)
                        .ConfigureAwait(false);

                await LogLoginSessionEndedAsync(accountId, loginSession.State).ConfigureAwait(false);
            }

            registry.Unregister(loginSession.SessionId);
            idleClock.Release(loginSession.SessionId);
            rateLimiter.Remove(loginSession.SessionId);
            admissionGate.Release();
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

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

    private void Greet(LoginClientSession session, SocketConnection connection)
    {
        var randomNumber = RandomNumberGenerator.GetInt32(GreetingRandomModulus) *
                           RandomNumberGenerator.GetInt32(GreetingRandomModulus);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        var packet = BuildGreetingPacket(randomNumber);
        session.Send(packet);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Login session {SessionId}: greeted (max {MaxPlayerNum}, present {PresentPlayerNum})",
                session.SessionId, packet.MaxPlayerNum, packet.PresentPlayerNum);
    }

    public LoginGreetingResponse BuildGreetingPacket(int randomNumber)
    {
        return new LoginGreetingResponse
        {
            RandomNumber = randomNumber,
            MaxPlayerNum = capacity.MaxPlayers,
            GagePlayerNum = capacity.GagePlayers,
            PresentPlayerNum = capacity.CurrentPlayers
        };
    }

    public override void Dispose()
    {
        _server?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
