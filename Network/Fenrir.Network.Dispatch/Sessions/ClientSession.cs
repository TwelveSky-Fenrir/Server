using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Logging;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Sessions;

// Owns the duplex pipe transport and the send-side lock; state-machine specifics live in the subclasses.
public abstract class ClientSession(
    long sessionId,
    IDuplexPipe transport,
    FenrirServer server,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : IPacketSession
{
    private const int SlowConsumerBackpressureStreakLimit = 5;

    /// <summary>
    ///     Frames that arrived while this session's own previous flush was still draining (socket
    ///     backpressure) -- see <see cref="Send{TPacket}" />/<see cref="SendRaw" />'s own remarks for why
    ///     <see cref="_sendLock" /> is only ever attempted, never waited on, from either entry point. Left
    ///     unbounded deliberately: the only thing that can make this grow without bound is a session whose
    ///     flush never resolves, and <see cref="SlowConsumerBackpressureStreakLimit" /> already tears such a
    ///     session down (see <see cref="ObserveFlushAsync" />) well before its queue could grow large -- a hard
    ///     capacity here would only risk silently dropping an ordinary gameplay packet (a chat line, a combat
    ///     result) during an otherwise-recoverable stall, which is worse than the bounded memory growth it
    ///     would save. <see cref="Channel{T}.Reader" /> is only ever drained by whichever call currently "owns"
    ///     this session's flush cycle -- the fast-path caller that just acquired <see cref="_sendLock" />, or
    ///     the <see cref="ObserveFlushAsync" /> continuation that inherited ownership from it -- so exactly one
    ///     logical reader is ever active even though that can't be expressed as a single call site;
    ///     <see cref="UnboundedChannelOptions.SingleWriter" /> is left at its default <see langword="false" />
    ///     since any thread may call <see cref="Send{TPacket}" />/<see cref="SendRaw" /> on a shared session
    ///     concurrently.
    /// </summary>
    private readonly Channel<byte[]> _pendingSends =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private int _backpressureStreak;
    private int _completed;

    public IDuplexPipe Transport { get; } = transport;

    public FenrirServer Server { get; } = server;

    /// <summary>The peer's address; null for a transport never backed by a real accepted socket.</summary>
    public IPEndPoint? RemoteEndPoint { get; } = remoteEndPoint;

    /// <summary>Legacy <c>mPacketEncryptionValue</c> 0 until the greeting packet seeds it.</summary>
    public byte InboundStreamXorKey { get; set; }

    public DisconnectReason? DisconnectReason { get; private set; }

    /// <summary>
    ///     UTC instant of this session's own last liveness signal: seeded to accept time (the connect-time
    ///     baseline a session that never sends anything is measured against) and re-stamped by <see cref="Touch" />
    ///     after every frame <see cref="SessionLoop" /> successfully dispatches. Read by a per-server idle-session
    ///     sweep to unilaterally disconnect a connection that stops producing traffic — see
    ///     <c>Fenrir.Application.Game.Domain.World.SessionLivenessSweep</c> (Server/ts25zone/S07_MyGame01.cpp:1963-2006)
    ///     for the GameServer policy that consumes this; deliberately generic/server-agnostic here since both
    ///     Login and Game sessions flow through the same <see cref="SessionLoop" />.
    /// </summary>
    public DateTimeOffset LastActivityUtc { get; private set; } = DateTimeOffset.UtcNow;

    /// <summary>Process-local, monotonically increasing — never persisted, never sent to the client.</summary>
    public long SessionId { get; } = sessionId;

    /// <summary>
    ///     Never blocks the calling thread -- even while this session's own previous flush is still draining
    ///     under socket backpressure. The most consequential caller is a zone's own single tick thread fanning
    ///     out a broadcast to many recipients in one synchronous pass; it must never stall on one recipient's
    ///     slow socket (see <see cref="_pendingSends" />'s own remarks for the full reasoning).
    /// </summary>
    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var total = FrameWriter.FrameSizeOf<TPacket>();

        if (!_sendLock.Wait(0))
        {
            // A previous Send/SendRaw on this same session hasn't finished flushing yet -- never wait for it
            // here. Copy this frame into its own buffer; whichever call currently owns the flush cycle drains
            // it, in order, once that flush resolves (FlushLocked/ObserveFlushAsync below).
            var queued = new byte[total];
            FrameWriter.WriteFrame(in packet, queued);
            _pendingSends.Writer.TryWrite(queued);
            ClaimOwnershipIfNowFreeToAvoidStrandedFrame();
            LogPacketSent(TPacket.Opcode, total);
            return;
        }

        try
        {
            var span = Transport.Output.GetSpan(total);
            FrameWriter.WriteFrame(in packet, span);
            Transport.Output.Advance(total);
        }
        catch
        {
            _sendLock.Release();
            throw;
        }

        LogPacketSent(TPacket.Opcode, total);
        FlushLocked();
    }

    /// <summary>Re-stamps <see cref="LastActivityUtc" /> to now — see that property's own remarks for callers/consumers.</summary>
    public void Touch()
    {
        LastActivityUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Checked by <see cref="SessionLoop" /> before dispatch against the generated
    ///     <c>SessionStateGate</c>.
    /// </summary>
    public abstract bool IsOpcodeAllowed(byte opcode);

    // Sends a fully pre-built frame as-is — the LZ4/ZPACKET path for Compressed M1 packets, whose bytes
    // already come out of the generated MessageFactory.Encode. Same never-block contract as Send<T> above.
    public void SendRaw(ReadOnlySpan<byte> rawFrame)
    {
        if (!_sendLock.Wait(0))
        {
            _pendingSends.Writer.TryWrite(rawFrame.ToArray());
            ClaimOwnershipIfNowFreeToAvoidStrandedFrame();
            LogPacketSent(OpcodeOf(rawFrame), rawFrame.Length);
            return;
        }

        try
        {
            var span = Transport.Output.GetSpan(rawFrame.Length);
            rawFrame.CopyTo(span);
            Transport.Output.Advance(rawFrame.Length);
        }
        catch
        {
            _sendLock.Release();
            throw;
        }

        LogPacketSent(OpcodeOf(rawFrame), rawFrame.Length);
        FlushLocked();
    }

    /// <summary>
    ///     Every producer of a pre-built raw frame (<see cref="FrameWriter.WriteFrame{TPacket}" />, the
    ///     generated <c>LoginMessageFactory</c>/<c>ZoneMessageFactory.Encode</c> ZPACKET envelope) writes the
    ///     opcode as the frame's first byte -- safe to read back here purely for <see cref="LogPacketSent" />,
    ///     without requiring every <see cref="SendRaw" /> call site to separately supply its own opcode.
    /// </summary>
    private static byte OpcodeOf(ReadOnlySpan<byte> rawFrame)
    {
        return rawFrame.IsEmpty ? (byte)0 : rawFrame[0];
    }

    /// <summary>
    ///     Debug-level operational log of an outgoing frame -- see <see cref="PacketLog.PacketSent" />'s own
    ///     remarks for why this relies on that method's generated <c>IsEnabled</c> check rather than an
    ///     explicit one here: <paramref name="opcode" />/<paramref name="byteSize" /> are already-known values
    ///     from the caller, so there is no extra work worth pre-gating.
    /// </summary>
    private void LogPacketSent(byte opcode, int byteSize)
    {
        logger?.PacketSent(SessionId, opcode, byteSize);
    }

    /// <summary>
    ///     Called by <see cref="LoginClientSession" />/<see cref="ZoneClientSession" />'s own Mark* state-
    ///     transition setters, one call per setter that actually changes <c>State</c> (a setter that only
    ///     records an ancillary fact -- e.g. <c>MarkAccountSessionToken</c> -- has nothing to log here).
    ///     Deliberately Information, not Debug: unlike <see cref="PacketLog" />'s per-packet convention, a
    ///     session-state transition happens at most a handful of times over a connection's whole lifetime
    ///     (Connected -&gt; ... -&gt; InWorld / HandoverIssued), so it is exactly the kind of infrequent,
    ///     high-signal event an operator watching live logs wants visible by default. Centralized here
    ///     (rather than left to each call site that happens to invoke a Mark* setter to also remember to log)
    ///     so coverage is structural, not incidental -- every transition is guaranteed to be observed exactly
    ///     once, regardless of whether the calling handler/service also logs its own business-outcome line
    ///     next to the same call.
    /// </summary>
    protected void LogSessionStateChanged<TState>(TState previousState, TState newState) where TState : struct, Enum
    {
        logger?.LogInformation(
            "Session {SessionId} ({Server}) state changed: {PreviousState} -> {NewState}",
            SessionId, Server, previousState, newState);
    }

    // Caller must already hold _sendLock. Flushes what was just written, then keeps draining/flushing
    // anything in _pendingSends before ever releasing the lock -- so a frame queued while this session was
    // catching up from backpressure is never reordered ahead of, or interleaved with, a later direct write.
    // Hands off to ObserveFlushAsync (without releasing the lock) the moment a flush doesn't complete
    // synchronously, exactly as before -- backpressure now only ever suspends that async continuation, never
    // a Send/SendRaw caller. Safe to call after only enqueuing (no direct write of your own first) --
    // see ClaimOwnershipIfNowFreeToAvoidStrandedFrame -- flushing with nothing new pending is a harmless no-op.
    private void FlushLocked()
    {
        while (true)
        {
            var flush = Transport.Output.FlushAsync();

            if (!flush.IsCompletedSuccessfully)
            {
                _ = ObserveFlushAsync(flush);
                return;
            }

            _backpressureStreak = 0;

            if (!TryWriteNextQueuedFrame())
            {
                _sendLock.Release();

                // Closes the release/enqueue TOCTOU race symmetrically to ClaimOwnershipIfNowFreeToAvoidStrandedFrame's
                // own claim-side check: a frame can be enqueued in the tiny window between the TryRead above
                // observing the queue empty and the Release just above actually running, and the enqueuer's own
                // Wait(0) attempt (in Send<T>/SendRaw's fast-fail branch, or ClaimOwnershipIfNowFreeToAvoidStrandedFrame)
                // can lose that race too if it lands before this Release completes -- stranding the frame with no
                // thread left obligated to drain it. Re-check here: if something is now pending, try to reclaim the
                // lock and resume draining. If another Send/SendRaw call wins the reclaim race instead, that call's
                // own FlushLocked drains the queue itself, so nothing is lost either way -- only this thread's own
                // obligation to drain ends.
                if (!_pendingSends.Reader.TryPeek(out _) || !_sendLock.Wait(0))
                    return;
            }
        }
    }

    // Caller must already hold _sendLock. Returns false (lock left untouched) when nothing was queued.
    private bool TryWriteNextQueuedFrame()
    {
        if (!_pendingSends.Reader.TryRead(out var frame))
            return false;

        try
        {
            var span = Transport.Output.GetSpan(frame.Length);
            frame.CopyTo(span);
            Transport.Output.Advance(frame.Length);
        }
        catch
        {
            _sendLock.Release();
            throw;
        }

        return true;
    }

    // Closes the release/enqueue race on the (already rare, already-allocating) losing side instead of on
    // every hot uncontended Send/SendRaw call: the owner that held _sendLock a moment ago may, by the time
    // this frame reached _pendingSends, have already observed the queue as empty and released -- with
    // nothing left to ever come back and drain this one. If the lock is free right this instant, claim it
    // and drain (this frame and anything else queued) ourselves; if it is still held, the current owner's
    // own in-progress FlushLocked loop has not yet reached its own "queue empty" check and will pick this up
    // normally, so there is nothing further to do.
    private void ClaimOwnershipIfNowFreeToAvoidStrandedFrame()
    {
        if (_sendLock.Wait(0))
            FlushLocked();
    }

    // A ValueTask<FlushResult> that doesn't complete synchronously means this send hit the pipe's
    // PauseWriterThreshold; FlushResult.IsCompleted/IsCanceled reflect reader completion/CancelPendingFlush,
    // not backpressure, so the synchronous-completion check above is the only backpressure signal available.
    private async ValueTask ObserveFlushAsync(ValueTask<FlushResult> flush)
    {
        bool keepDraining;

        try
        {
            var result = await flush.ConfigureAwait(false);
            keepDraining = result is { IsCanceled: false, IsCompleted: false };

            if (keepDraining && ++_backpressureStreak >= SlowConsumerBackpressureStreakLimit)
            {
                // Fires at most once per aborted session -- only when the streak actually trips the limit,
                // never on every non-synchronous flush -- so this is nowhere near the per-packet hot path
                // PacketLog exists to guard. Without this, an operator watching live logs sees this session's
                // socket simply close with no detail beyond SessionLoop's own terminal "connection loop ended,
                // disconnect reason SlowConsumer" line, which carries no indication of *why* it was flagged.
                logger?.LogWarning(
                    "Session {SessionId}: aborting as a slow consumer -- {Streak} consecutive non-synchronous TX flushes",
                    SessionId, _backpressureStreak);
                Abort(Sessions.DisconnectReason.SlowConsumer);
                keepDraining = false;
            }
        }
        catch (Exception)
        {
            // Receive loop will independently notice the broken pipe; a failed flush here must not fault the
            // caller's Send<T>/SendRaw -- both already returned by the time this continuation runs.
            keepDraining = false;
        }

        if (!keepDraining)
        {
            _sendLock.Release();
            return;
        }

        // The flush that was in flight when this session's lock was last handed off here has now resolved and
        // the session is still healthy -- resume draining (still holding _sendLock) so anything queued while
        // it was pending reaches the pipe before a fresh Send/SendRaw can observe the lock as free.
        try
        {
            if (TryWriteNextQueuedFrame())
                FlushLocked();
            else
                _sendLock.Release();
        }
        catch
        {
            // TryWriteNextQueuedFrame already released _sendLock before rethrowing; this discarded fire-and-
            // forget continuation must not propagate further.
        }
    }

    /// <summary>Idempotent: only the first caller's <paramref name="reason" /> sticks.</summary>
    public void Abort(DisconnectReason reason)
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        DisconnectReason = reason;
        Transport.Input.CancelPendingRead();
        Transport.Output.CancelPendingFlush();
    }

    public async ValueTask CompleteAsync()
    {
        await Transport.Input.CompleteAsync().ConfigureAwait(false);
        await Transport.Output.CompleteAsync().ConfigureAwait(false);
    }
}
