using System.Buffers;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Framing;
using Fenrir.Network.RateLimiting;
using Fenrir.Network.Sessions;

namespace Fenrir.Network.Dispatching;

// Any malformed frame/unknown opcode/illegal state/rate-limit breach ends the session — matches the
// legacy server's own "any violation closes the socket" posture.
public static class SessionLoop
{
    public static async Task RunAsync(
        ClientSession session,
        IFrameDispatcher dispatcher,
        ISessionRateLimiter? rateLimiter,
        CancellationToken cancellationToken)
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
                    await ProcessBufferAsync(session, dispatcher, rateLimiter, result.Buffer, cancellationToken)
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
        ReadOnlySequence<byte> buffer,
        CancellationToken cancellationToken)
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
            catch (ProtocolViolationException)
            {
                session.Abort(DisconnectReason.UnknownOpcode);
                return new BufferOutcome(remaining.Start, remaining.End, true);
            }

            if (!decoded)
                return new BufferOutcome(remaining.Start, remaining.End, false); // partial frame — wait for more bytes

            if (!session.IsOpcodeAllowed(frameOpcode))
            {
                session.Abort(DisconnectReason.StateViolation);
                return new BufferOutcome(remaining.Start, remaining.End, true);
            }

            if (rateLimiter is not null && !rateLimiter.TryConsume(session.SessionId, frameServer, frameOpcode))
            {
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
