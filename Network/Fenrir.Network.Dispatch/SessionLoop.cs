using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch;

// Any malformed frame/unknown opcode/illegal state/rate-limit breach ends the session — matches the
// legacy server's own "any violation closes the socket" posture.
public static class SessionLoop
{
    public static async Task RunAsync(
        ClientSession session,
        IFrameDispatcher dispatcher,
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
                    break; // Abort() called (externally or by a previous iteration) — reason already recorded

                var outcome =
                    await ProcessBufferAsync(session, dispatcher, rateLimiter, ipFloodGuard, result.Buffer,
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
            // Server shutdown or an external Abort() racing the read — either way, shut down cleanly below.
        }
        finally
        {
            await session.CompleteAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask<BufferOutcome> ProcessBufferAsync(
        ClientSession session,
        IFrameDispatcher dispatcher,
        ISessionRateLimiter? rateLimiter,
        IpFloodGuard? ipFloodGuard,
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken,
        ILogger? logger)
    {
        // Copied to a local: ref-safety for a ref struct out-param is stricter on an async method's parameter than a local.
        var remaining = buffer;

        while (true)
        {
            FenrirServer frameServer;
            byte frameOpcode;
            ReadOnlySequence<byte> framePayload;
            bool decoded;

            try
            {
                decoded = FrameDecoder.TryReadFrame(ref remaining, session.Server, out var frame);
                frameServer = frame.Server;
                frameOpcode = frame.Opcode;
                framePayload = frame.Payload;
            }
            catch (ProtocolViolationException ex)
            {
                // Trigger B (contract): an unrecognized opcode from an already-connected session is the
                // protocol-violation flood counter's input. Guard is optional so unit tests exercising just
                // FrameDecoder/state-gate/rate-limit behavior don't need one wired up.
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
                return new BufferOutcome(remaining.Start, remaining.End, false); // partial frame — wait for more bytes

            logger?.LogDebug("Session {SessionId}: frame received, server {Server} opcode {Opcode} ({Length} bytes)",
                session.SessionId, frameServer, frameOpcode, framePayload.Length);

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

            await dispatcher.DispatchAsync(frameServer, frameOpcode, framePayload, session, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private readonly struct BufferOutcome(SequencePosition consumed, SequencePosition examined, bool shouldStop)
    {
        public SequencePosition Consumed { get; } = consumed;
        public SequencePosition Examined { get; } = examined;
        public bool ShouldStop { get; } = shouldStop;
    }
}
