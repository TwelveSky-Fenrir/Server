using System.Buffers;
using System.Diagnostics;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Logging;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch;

public static class SessionLoop
{
    public static async Task RunConnectionAsync(
        SocketConnection connection,
        ClientSession session,
        IFrameDispatcher dispatcher,
        IOpcodeFrameSizeProvider registry,
        ISessionRateLimiter? rateLimiter,
        IpFloodGuard? ipFloodGuard,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        var ioTask = connection.RunIoAsync(cancellationToken);

        try
        {
            await RunAsync(session, dispatcher, registry, rateLimiter, ipFloodGuard, cancellationToken, logger)
                .ConfigureAwait(false);
        }
        finally
        {
            connection.Abort();

            try
            {
                await ioTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex,
                    "Session {SessionId} ({Server}, {RemoteEndPoint}): transport I/O loop ended with an exception while tearing the connection down",
                    session.SessionId, session.Server, session.RemoteEndPoint);
            }
        }
    }

    public static async Task RunAsync(
        ClientSession session,
        IFrameDispatcher dispatcher,
        IOpcodeFrameSizeProvider registry,
        ISessionRateLimiter? rateLimiter,
        IpFloodGuard? ipFloodGuard,
        CancellationToken cancellationToken,
        ILogger? logger = null)
    {
        var reader = session.Transport.Input;

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                    break;

                var outcome =
                    await ProcessBufferAsync(session, dispatcher, registry, rateLimiter, ipFloodGuard, result.Buffer,
                            cancellationToken, logger)
                        .ConfigureAwait(false);

                reader.AdvanceTo(outcome.Consumed, outcome.Examined);

                if (outcome.ShouldStop)
                    break;

                if (!result.IsCompleted) continue;
                session.Abort(DisconnectReason.ClientClosed);
                break;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (session.DisconnectReason is { } reason)
                logger?.LogInformation(
                    "Session {SessionId} ({Server}, {RemoteEndPoint}): connection loop ended, disconnect reason {DisconnectReason}",
                    session.SessionId, session.Server, session.RemoteEndPoint, reason);
            else
                logger?.LogDebug(
                    "Session {SessionId} ({Server}, {RemoteEndPoint}): connection loop ended without an explicit disconnect reason",
                    session.SessionId, session.Server, session.RemoteEndPoint);

            await session.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask<BufferOutcome> ProcessBufferAsync(
        ClientSession session,
        IFrameDispatcher dispatcher,
        IOpcodeFrameSizeProvider registry,
        ISessionRateLimiter? rateLimiter,
        IpFloodGuard? ipFloodGuard,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken,
        ILogger? logger)
    {
        var remaining = buffer;

        var debugEnabled = logger is not null && logger.IsEnabled(LogLevel.Debug);

        while (true)
        {
            if (session.DisconnectReason is not null)
                return new BufferOutcome(remaining.Start, remaining.End, true);

            var frameStart = remaining.Start;
            FenrirServer frameServer;
            byte frameOpcode;
            ReadOnlySequence<byte> framePayload;
            bool decoded;
            var decodeStartTimestamp = debugEnabled ? Stopwatch.GetTimestamp() : 0L;

            try
            {
                decoded = FrameReader.TryReadFrame(ref remaining, registry, session.Server, out var frame);
                frameServer = frame.Server;
                frameOpcode = frame.Opcode;
                framePayload = frame.Payload;
            }
            catch (ProtocolViolationException ex)
            {
                if (ipFloodGuard is not null && session.RemoteEndPoint is not null)
                    await ipFloodGuard
                        .RecordProtocolViolationAsync(session.RemoteEndPoint.Address.ToString(), cancellationToken)
                        .ConfigureAwait(false);

                logger?.LogWarning(
                    "Session {SessionId} ({RemoteEndPoint}): unknown opcode {Opcode} for server {Server} -- aborting",
                    session.SessionId, session.RemoteEndPoint, ex.Opcode, ex.Server);
                session.Abort(DisconnectReason.UnknownOpcode);
                return new BufferOutcome(remaining.Start, remaining.End, true);
            }

            if (!decoded)
                return new BufferOutcome(remaining.Start, remaining.End, false);

            if (debugEnabled)
                logger!.PacketReceived(session.SessionId, frameServer, frameOpcode, (int)framePayload.Length,
                    Stopwatch.GetElapsedTime(decodeStartTimestamp).TotalMicroseconds);

            if (!session.IsOpcodeAllowed(frameOpcode))
            {
                logger?.LogWarning(
                    "Session {SessionId} ({RemoteEndPoint}): opcode {Opcode} not allowed in the session's current state -- aborting",
                    session.SessionId, session.RemoteEndPoint, frameOpcode);
                session.Abort(DisconnectReason.StateViolation);
                return new BufferOutcome(remaining.Start, remaining.End, true);
            }

            if (rateLimiter is not null && !rateLimiter.TryConsume(session.SessionId, frameServer, frameOpcode))
            {
                logger?.LogWarning(
                    "Session {SessionId} ({RemoteEndPoint}): opcode {Opcode} rate-limited -- aborting",
                    session.SessionId, session.RemoteEndPoint, frameOpcode);
                session.Abort(DisconnectReason.RateLimited);
                return new BufferOutcome(remaining.Start, remaining.End, true);
            }

            try
            {
                var metricsEnabled = DispatchMetrics.DispatchDurationMs.Enabled;
                var dispatchStartTimestamp = debugEnabled || metricsEnabled ? Stopwatch.GetTimestamp() : 0L;

                var dispatchOutcome = await dispatcher
                    .DispatchAsync(frameServer, frameOpcode, framePayload, session, cancellationToken)
                    .ConfigureAwait(false);

                if (dispatchOutcome == FrameDispatchOutcome.Withheld)
                    return new BufferOutcome(frameStart, remaining.End, false);

                if (debugEnabled)
                    logger!.PacketDispatched(session.SessionId, frameServer, frameOpcode,
                        Stopwatch.GetElapsedTime(dispatchStartTimestamp).TotalMicroseconds);

                if (metricsEnabled)
                    DispatchMetrics.DispatchDurationMs.Record(
                        Stopwatch.GetElapsedTime(dispatchStartTimestamp).TotalMilliseconds,
                        DispatchMetrics.ServerTag(frameServer));

                if (DispatchMetrics.PacketsDispatched.Enabled)
                    DispatchMetrics.PacketsDispatched.Add(1, DispatchMetrics.ServerTag(frameServer));

                session.Touch();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "Session {SessionId} ({RemoteEndPoint}): unhandled exception dispatching {Server} opcode {Opcode} -- aborting",
                    session.SessionId, session.RemoteEndPoint, frameServer, frameOpcode);
                session.Abort(DisconnectReason.Faulted);
                return new BufferOutcome(remaining.Start, remaining.End, true);
            }
        }
    }

    private readonly struct BufferOutcome(SequencePosition consumed, SequencePosition examined, bool shouldStop)
    {
        public SequencePosition Consumed { get; } = consumed;
        public SequencePosition Examined { get; } = examined;
        public bool ShouldStop { get; } = shouldStop;
    }
}
