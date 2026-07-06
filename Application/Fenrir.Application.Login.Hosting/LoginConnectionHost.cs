using System.Net;
using System.Security.Cryptography;
using Fenrir.Application.Login.Domain;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
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
    ILogger<LoginConnectionHost> logger) : BackgroundService
{
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

        try
        {
            await GreetAsync(loginSession, connection, ct).ConfigureAwait(false);

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
            if (loginSession.AccountId is { } accountId)
                await TearDownAccountSessionAsync(accountId, loginSession.AccountSessionToken).ConfigureAwait(false);

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
