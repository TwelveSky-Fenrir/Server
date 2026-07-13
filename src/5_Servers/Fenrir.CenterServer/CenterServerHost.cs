using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using Fenrir.Cluster;
using Fenrir.Core.Wire;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Framing;
using Fenrir.Network.Transport;
using Fenrir.Security.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.CenterServer;

internal sealed class CenterServerHost(
    ILogger<CenterServerHost> logger,
    IOptions<CenterServerOptions> options,
    ICenterLinkAuthenticator authenticator,
    IFrameDispatcher dispatcher)
    : BackgroundService
{
    private readonly ConcurrentDictionary<Task, byte> _inFlightLinks = new();

    private FenrirTcpListener<CenterLinkSession>? _listener;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        _listener = new FenrirTcpListener<CenterLinkSession>(
            new IPEndPoint(IPAddress.Any, opts.Port),
            (sessionId, transport, remoteEndPoint) =>
                new CenterLinkSession(sessionId, transport, remoteEndPoint, logger),
            logger);

        logger.LogInformation(
            "Fenrir.CenterServer listening on :{Port} (internal S2S, passive; accepts Zone/Login links, opens no " +
            "outbound game TCP). S2S auth: {AuthState}.",
            opts.Port,
            authenticator.IsEnabled
                ? "HMAC shared-secret handshake ENABLED (hardens legacy flaw #8)"
                : "DISABLED -- no shared secret configured; every link is refused (fail-closed)");

        try
        {
            await _listener.AcceptLoopAsync(TrackInFlightLink, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        var outstanding = _inFlightLinks.Keys.ToArray();
        if (outstanding.Length == 0)
            return;

        try
        {
            await Task.WhenAll(outstanding).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "CenterServer shutdown proceeding with {Count} S2S link teardown(s) still in flight", outstanding.Length);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "One or more S2S links faulted while tearing down during shutdown");
        }
    }

    private Task TrackInFlightLink(CenterLinkSession session, SocketConnection connection, CancellationToken ct)
    {
        var task = RunLinkAsync(session, connection, ct);

        _inFlightLinks[task] = 0;
        _ = task.ContinueWith(t => _inFlightLinks.TryRemove(t, out _), TaskScheduler.Default);

        return task;
    }

    private async Task RunLinkAsync(CenterLinkSession session, SocketConnection connection, CancellationToken ct)
    {
        var remoteIp = session.RemoteEndPoint?.Address.ToString();
        logger.LogInformation("S2S link {SessionId} accepted from {RemoteIp}", session.SessionId, remoteIp);

        var ioTask = connection.RunIoAsync(ct);

        try
        {
            if (await TryAuthenticateAsync(session, connection, ct).ConfigureAwait(false))
                await DispatchLoopAsync(session, connection, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (TransportFaultClassifier.IsExpectedDisconnect(ex))
                logger.LogInformation("S2S link {SessionId} disconnected ({ExceptionType}: {Message})",
                    session.SessionId, ex.GetType().Name, ex.Message);
            else
                logger.LogError(ex, "S2S link {SessionId} ended abnormally due to an unhandled exception",
                    session.SessionId);
        }
        finally
        {
            connection.Abort();
            await ioTask.ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            logger.LogInformation("S2S link {SessionId} from {RemoteIp} closed", session.SessionId, remoteIp);
        }
    }

        private async Task<bool> TryAuthenticateAsync(CenterLinkSession session, SocketConnection connection,
        CancellationToken ct)
    {
        if (!authenticator.IsEnabled)
        {
            logger.LogError(
                "S2S link {SessionId} refused: CenterServer shared secret not configured (fail-closed handshake)",
                session.SessionId);
            return false;
        }

        var challenge = authenticator.IssueChallenge();
        session.SendRaw(challenge.Nonce);

        var response = await ReadExactAsync(connection.Input, CenterLinkAuth.MacSize, ct).ConfigureAwait(false);
        if (response is null)
        {
            logger.LogWarning("S2S link {SessionId} closed before completing the HMAC handshake", session.SessionId);
            return false;
        }

        if (!authenticator.VerifyHelloMac(in challenge, ReadOnlySpan<byte>.Empty, response))
        {
            logger.LogWarning("S2S link {SessionId} failed the HMAC handshake -- refusing the peer", session.SessionId);
            return false;
        }

        session.MarkAuthenticated();
        logger.LogInformation("S2S link {SessionId} authenticated", session.SessionId);
        return true;
    }

        private async Task DispatchLoopAsync(CenterLinkSession session, SocketConnection connection, CancellationToken ct)
    {
        var reader = connection.Input;

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(ct).ConfigureAwait(false);
                if (result.IsCanceled)
                    break;

                var buffer = result.Buffer;

                try
                {
                    while (S2SFrameReader.TryReadFrame(ref buffer, CenterOpcodeRegistry.Provider, FenrirServer.Center,
                               out var frame))
                    {
                        var opcode = frame.Opcode;
                        var payload = frame.Payload;

                        if (!session.IsOpcodeAllowed(opcode))
                        {
                            logger.LogWarning(
                                "S2S link {SessionId}: opcode {Opcode} not allowed in state {State} -- closing",
                                session.SessionId, opcode, session.State);
                            return;
                        }

                        await dispatcher.DispatchAsync(FenrirServer.Center, opcode, payload, session, ct)
                            .ConfigureAwait(false);
                    }
                }
                catch (Fenrir.Network.Framing.ProtocolViolationException ex)
                {
                    logger.LogWarning(
                        "S2S link {SessionId}: unknown opcode {Opcode} for {Server} -- closing",
                        session.SessionId, ex.Opcode, ex.Server);
                    return;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
    }

        private static async Task<byte[]?> ReadExactAsync(PipeReader reader, int count, CancellationToken ct)
    {
        while (true)
        {
            var result = await reader.ReadAsync(ct).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (buffer.Length >= count)
            {
                var slice = buffer.Slice(0, count);
                var bytes = slice.ToArray();
                reader.AdvanceTo(slice.End);
                return bytes;
            }

            if (result.IsCompleted || result.IsCanceled)
            {
                reader.AdvanceTo(buffer.Start, buffer.End);
                return null;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    public override void Dispose()
    {
        _listener?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        base.Dispose();
    }
}
