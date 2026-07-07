using System.Buffers;
using System.Diagnostics;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Logging;
using Fenrir.Network.Dispatch.RateLimiting;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch;

// Any malformed frame/unknown opcode/illegal state/rate-limit breach ends the session — matches the
// legacy server's own "any violation closes the socket" posture. An uncaught handler exception is
// treated the same way (see the dispatch try/catch below): neither ZoneFrameDispatcher nor
// LoginFrameDispatcher wraps its own MessageDispatcher calls, so this is the one place shared by both
// servers where such an exception can be caught, logged with full context, and turned into a clean,
// reason-recorded session teardown instead of an unplanned propagation all the way out of RunAsync.
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

        // Hoisted out of the frame loop below (one buffer read can contain several frames): a virtual
        // IsEnabled call is cheap, but there's no reason to repeat it per frame when the logger itself never
        // changes for the lifetime of this call. Checked explicitly (rather than relying on PacketReceived's
        // own generated IsEnabled check) because it also gates the Stopwatch capture just below -- see
        // PacketLog.PacketReceived's own remarks for why that capture must not happen unconditionally.
        var debugEnabled = logger is not null && logger.IsEnabled(LogLevel.Debug);

        while (true)
        {
            FenrirServer frameServer;
            byte frameOpcode;
            ReadOnlySequence<byte> framePayload;
            bool decoded;
            var decodeStartTimestamp = debugEnabled ? Stopwatch.GetTimestamp() : 0L;

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

            if (debugEnabled)
                // framePayload.Length is a long (ReadOnlySequence<byte>'s own contract) but every real wire
                // frame is header + a handful to a few thousand payload bytes -- OpcodeRegistry.FrameSizeOf's
                // own int-typed size table already bounds it well under int range, so this narrowing is safe.
                logger!.PacketReceived(session.SessionId, frameServer, frameOpcode, (int)framePayload.Length,
                    Stopwatch.GetElapsedTime(decodeStartTimestamp).TotalMicroseconds);

            // Known architectural limitation, not a bug: IsOpcodeAllowed / the generated SessionStateGate it
            // delegates to can only gate on the static session-state enum value (LoginSessionState /
            // ZoneSessionState), never on a state+runtime-flag combination (e.g. "InGame AND actively
            // dueling"). Any precondition that needs a live runtime flag on top of session state has to be
            // checked inside the packet's own handler after dispatch, not here. See
            // SessionStateGateEmitter's own remarks (Generators/Fenrir.Generators.Protocol/Emitters/
            // SessionStateGateEmitter.cs) for the full rationale -- this is a documented forward-looking
            // limitation only; no live Fenrir feature currently needs a combined gate at this layer.
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
                await dispatcher.DispatchAsync(frameServer, frameOpcode, framePayload, session, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cooperative cancellation (server shutdown, or an external Abort() racing this dispatch) is
                // not a handler fault -- let it propagate so RunAsync's own catch handles it as a clean shutdown.
                throw;
            }
            catch (Exception ex)
            {
                // No packet handler is allowed to bring down the whole session loop uncaught. Without this,
                // the exception would propagate through ZoneFrameDispatcher/LoginFrameDispatcher (neither
                // wraps its own MessageDispatcher calls) and out of RunAsync itself, surfacing only as a
                // Debug-level "session ended" log at the connection host with no disconnect-reason recorded
                // at all -- unlike every other violation in this loop. Treat it the same way: log loudly here,
                // record a reason, and end the session cleanly instead of letting the stack unwind uncontrolled.
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
