using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Login;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Network.Dispatching;
using Fenrir.Network.RateLimiting;
using Fenrir.Network.Sessions;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Options;

namespace Fenrir.LoginServer;

/// <summary>
///     Owns the login listen socket for the process lifetime: binds <see cref="FenrirTcpListener" /> to
///     <see cref="LoginServerOptions.Port" />, and for each accepted connection greets the client
///     (<c>LC_LOGIN_CONNECT_OK_RECV</c>, wire contract §4.1), seeds the inbound stream cipher (§3.4), then runs
///     the connection's I/O pump and <see cref="Fenrir.Network.Dispatching.SessionLoop" /> side by side until
///     either ends.
/// </summary>
public sealed class LoginConnectionHost(
    IOptions<LoginServerOptions> options,
    IFrameDispatcher dispatcher,
    ISessionRateLimiter rateLimiter,
    SessionRegistry registry,
    ILogger<LoginConnectionHost> logger) : BackgroundService
{
    private FenrirTcpListener? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        _listener = new FenrirTcpListener(
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
            // Normal shutdown path.
        }
    }

    private async Task OnAcceptedAsync(ClientSession session, SocketConnection connection, CancellationToken ct)
    {
        var loginSession = (LoginClientSession)session;
        registry.Register(loginSession);

        try
        {
            Greet(loginSession, connection);

            await Task.WhenAll(
                connection.RunIOAsync(ct),
                SessionLoop.RunAsync(loginSession, dispatcher, rateLimiter, ct)
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Login session {SessionId} ended", loginSession.SessionId);
        }
        finally
        {
            registry.Unregister(loginSession.SessionId);
            rateLimiter.Remove(loginSession.SessionId);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Sends the op 0 greeting and seeds <c>mPacketEncryptionValue</c> (§3.4) BEFORE the connection's I/O pump
    ///     starts, so no inbound byte is ever decoded with the wrong (unseeded) key. The greeting itself is safe to
    ///     write before <see cref="SocketConnection.RunIOAsync" /> starts: <see cref="ClientSession.Send{TPacket}" />
    ///     only buffers into the in-memory TX pipe, which the (not-yet-started) send loop drains once running.
    /// </summary>
    private void Greet(LoginClientSession session, SocketConnection connection)
    {
        // rand_nor()%1001 * rand_nor()%1001 (legacy) is not reproducible/needed byte-for-byte — only the
        // low byte (the stream-cipher key) and the full int (sent to the client) need to look random.
        var randomNumber = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

        session.InboundStreamXorKey = unchecked((byte)randomNumber);
        connection.GetInboundXorKey = () => session.InboundStreamXorKey;

        session.Send(new LoginGreetingResponse
        {
            RandomNumber = randomNumber,
            MaxPlayerNum = options.Value.MaxPlayerNum,
            GagePlayerNum = 0,
            PresentPlayerNum = registry.Count
        });
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
