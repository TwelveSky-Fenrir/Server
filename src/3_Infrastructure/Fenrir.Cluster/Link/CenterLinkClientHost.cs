using System.IO.Pipelines;
using System.Net.Sockets;
using Fenrir.Core.Abstractions;
using Fenrir.Core.Wire;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Transport;
using Fenrir.Security.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Cluster.Link;

/// <summary>
///     Maintains the persistent OUTBOUND link from a LoginServer/GameServer to the CenterServer, and provides the
///     <see cref="ICenterLink" /> send API over it. The mirror of <c>CenterServerHost</c>: it dials the Center,
///     completes the HMAC handshake in reverse (receive nonce -> compute MAC -> send MAC), then pumps outbound sends
///     and inbound fan-out until the link drops, at which point it reconnects with exponential backoff.
///     <para>
///         Boot is LAZY and never blocking: this is a <see cref="BackgroundService" />, so the host starts and accepts
///         its own clients even while the Center is unreachable. There is no <c>WaitFor(center)</c> anywhere — the
///         uplink simply comes up whenever it can.
///     </para>
/// </summary>
internal sealed class CenterLinkClientHost(
    ILogger<CenterLinkClientHost> logger,
    IOptions<CenterLinkClientOptions> options,
    ICenterLinkAuthenticator authenticator,
    IFrameDispatcher? inboundDispatcher)
    : BackgroundService, ICenterLink
{
    private volatile CenterUplinkSession? _session;
    private long _sessionSeq;

    public bool IsConnected => _session is not null;

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var session = _session;
        if (session is null)
            return;

        try
        {
            // ClientSession.Send already no-ops once the session is aborted, so a session captured
            // mid-teardown is safe; the try/catch only guards the vanishingly small window where the TX pipe
            // is completed out from under us during teardown. A best-effort uplink send must never disrupt the
            // caller's thread (a zone tick / handler thread).
            session.Send(in packet);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "CenterLink send dropped (uplink tearing down)");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        if (!authenticator.IsEnabled)
        {
            logger.LogWarning(
                "CenterLink uplink dormant: no shared secret configured (Center__SharedSecret). The host serves its " +
                "own clients normally; no Center uplink will be attempted.");
            return;
        }

        if (!TryParseEndpoint(opts.Endpoint, out var host, out var port))
        {
            logger.LogWarning(
                "CenterLink uplink dormant: Center endpoint missing or unparseable ('{Endpoint}'). Expected " +
                "'host:port' or 'tcp://host:port'.",
                opts.Endpoint);
            return;
        }

        var initialDelay = TimeSpan.FromMilliseconds(Math.Max(1, opts.InitialReconnectDelayMilliseconds));
        var maxDelay = TimeSpan.FromMilliseconds(
            Math.Max(opts.InitialReconnectDelayMilliseconds, opts.MaxReconnectDelayMilliseconds));
        var delay = initialDelay;

        logger.LogInformation(
            "CenterLink uplink starting: maintaining a persistent authenticated S2S link to {Host}:{Port}", host, port);

        while (!stoppingToken.IsCancellationRequested)
        {
            var authenticated = await TryRunLinkAsync(host, port, opts, stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
                break;

            // A link that actually came up resets the backoff, so a later drop reconnects promptly; repeated
            // connect/handshake failures keep doubling the delay toward the ceiling.
            if (authenticated)
                delay = initialDelay;

            try
            {
                await Task.Delay(WithJitter(delay), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!authenticated)
                delay = TimeSpan.FromMilliseconds(Math.Min(delay.TotalMilliseconds * 2, maxDelay.TotalMilliseconds));
        }

        logger.LogInformation("CenterLink uplink stopped");
    }

    /// <summary>
    ///     Runs one connect -> handshake -> receive-loop attempt end to end. Returns true iff the link
    ///     authenticated (used by the caller to decide whether to reset the backoff). Never throws — every fault is
    ///     classified and logged here, and teardown is symmetric with <c>CenterServerHost.RunLinkAsync</c>'s finally.
    /// </summary>
    private async Task<bool> TryRunLinkAsync(string host, int port, CenterLinkClientOptions opts, CancellationToken ct)
    {
        Socket? socket = null;
        SocketConnection? connection = null;
        CenterUplinkSession? session = null;
        Task? ioTask = null;
        var authenticated = false;

        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                connectCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, opts.ConnectTimeoutMilliseconds)));
                await socket.ConnectAsync(host, port, connectCts.Token).ConfigureAwait(false);
            }

            connection = new SocketConnection(socket, logger);
            socket = null; // ownership transferred: SocketConnection.DisposeAsync now owns the socket.
            ioTask = connection.RunIoAsync(ct);

            session = new CenterUplinkSession(Interlocked.Increment(ref _sessionSeq), connection, logger);

            if (!await PerformHandshakeAsync(session, connection, ct).ConfigureAwait(false))
                return false;

            authenticated = true;
            _session = session;
            logger.LogInformation(
                "CenterLink up: authenticated uplink to {Host}:{Port} (session {SessionId})", host, port,
                session.SessionId);

            await ReceiveLoopAsync(session, connection, ct).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return authenticated;
        }
        catch (Exception ex)
        {
            if (TransportFaultClassifier.IsExpectedDisconnect(ex))
                logger.LogInformation(
                    "CenterLink to {Host}:{Port} dropped ({Type}: {Message}); reconnecting", host, port,
                    ex.GetType().Name, ex.Message);
            else if (authenticated)
                logger.LogWarning(ex, "CenterLink to {Host}:{Port} faulted after authenticating; reconnecting", host,
                    port);
            else
                logger.LogDebug(ex, "CenterLink connect/handshake to {Host}:{Port} failed; retrying with backoff", host,
                    port);

            return authenticated;
        }
        finally
        {
            _session = null;
            session?.Abort(DisconnectReason.ClientClosed);

            if (connection is not null)
            {
                // Same teardown ordering as CenterServerHost: Abort() unblocks the in-flight socket read, then we
                // join RunIoAsync before disposing. RunIoAsync's loops never throw out (they encode any failure
                // into pipe completion), so awaiting it here is safe without a guard.
                connection.Abort();
                if (ioTask is not null)
                    await ioTask.ConfigureAwait(false);

                await connection.DisposeAsync().ConfigureAwait(false);
            }

            socket?.Dispose(); // non-null only if SocketConnection construction threw before taking ownership.
        }
    }

    /// <summary>
    ///     Client half of the handshake, the exact reverse of <c>CenterServerHost.TryAuthenticateAsync</c>: receive
    ///     the raw <see cref="CenterLinkAuth.NonceSize" />-byte nonce, compute the HMAC over it (empty context, same
    ///     as the server verifies), and send the raw <see cref="CenterLinkAuth.MacSize" />-byte MAC back. Nonce/MAC
    ///     travel un-framed and in clear, identical to the accept side.
    /// </summary>
    private async Task<bool> PerformHandshakeAsync(CenterUplinkSession session, SocketConnection connection,
        CancellationToken ct)
    {
        var nonce = await ReadExactAsync(connection.Input, CenterLinkAuth.NonceSize, ct).ConfigureAwait(false);
        if (nonce is null)
        {
            logger.LogWarning(
                "CenterLink handshake: Center closed before sending the {NonceSize}-byte challenge nonce",
                CenterLinkAuth.NonceSize);
            return false;
        }

        var mac = new byte[CenterLinkAuth.MacSize];
        authenticator.ComputeHelloMac(nonce, ReadOnlySpan<byte>.Empty, mac);
        session.SendRaw(mac);
        return true;
    }

    /// <summary>
    ///     Decodes the CenterServer's fan-out stream with <see cref="S2SFrameReader" /> + the generated
    ///     <c>CenterOpcodeRegistry.Provider</c> (1-byte opcode header), mirroring <c>CenterServerHost</c>'s dispatch
    ///     loop. Each frame is routed to the optional inbound <see cref="IFrameDispatcher" />; if none is wired (the
    ///     default this lot — no client-side inbound handlers exist yet), the frame is logged and dropped.
    /// </summary>
    private async Task ReceiveLoopAsync(CenterUplinkSession session, SocketConnection connection, CancellationToken ct)
    {
        var reader = connection.Input;

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
                    // Pull the ref-struct Frame's fields into ordinary locals before any await.
                    var opcode = frame.Opcode;
                    var payload = frame.Payload;

                    if (inboundDispatcher is not null)
                        await inboundDispatcher.DispatchAsync(FenrirServer.Center, opcode, payload, session, ct)
                            .ConfigureAwait(false);
                    else
                        logger.LogDebug(
                            "CenterLink received Center fan-out opcode {Opcode} ({Length} bytes) but no inbound " +
                            "IFrameDispatcher is wired -- dropping", opcode, payload.Length);
                }
            }
            catch (Fenrir.Network.Framing.ProtocolViolationException ex)
            {
                logger.LogWarning(
                    "CenterLink: unrecognized fan-out opcode {Opcode} for {Server} -- closing uplink", ex.Opcode,
                    ex.Server);
                break;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
                break;
        }
    }

    /// <summary>Reads exactly <paramref name="count" /> raw bytes, or null if the peer closes first (handshake use).</summary>
    private static async Task<byte[]?> ReadExactAsync(PipeReader reader, int count, CancellationToken ct)
    {
        while (true)
        {
            var result = await reader.ReadAsync(ct).ConfigureAwait(false);
            var buffer = result.Buffer;

            if (buffer.Length >= count)
            {
                var slice = buffer.Slice(0, count);
                var bytes = System.Buffers.BuffersExtensions.ToArray(in slice);
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

    private static TimeSpan WithJitter(TimeSpan delay)
    {
        // Up to +20% jitter so a fleet of shards restarting together doesn't stampede the Center in lockstep.
        var jitterCeiling = (int)(delay.TotalMilliseconds * 0.2) + 1;
        return delay + TimeSpan.FromMilliseconds(Random.Shared.Next(0, jitterCeiling));
    }

    private static bool TryParseEndpoint(string? endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint))
            return false;

        var value = endpoint.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
            value = "tcp://" + value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host) || uri.Port <= 0)
            return false;

        host = uri.Host;
        port = uri.Port;
        return true;
    }
}
