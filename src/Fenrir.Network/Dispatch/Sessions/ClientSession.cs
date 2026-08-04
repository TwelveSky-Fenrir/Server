using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using System.Diagnostics;
using Fenrir.Network.Dispatch.Logging;
using Fenrir.Network.Framing;
using Fenrir.Network.Transport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Sessions;

public abstract class ClientSession : IPacketSession
{
    private const int SlowConsumerBackpressureStreakLimit = 5;
    private const int LoginMaxPendingSendBytes = 40_960;
    private const int ZoneMaxPendingSendBytes = 1_024_000;
    private const int LoginMaxPendingSendFrames = 128;
    private const int ZoneMaxPendingSendFrames = 1_024;
    private const int SendPumpDrainTimeoutMs = 5_000;
    private const int NoDisconnectReason = -1;

    private readonly IBufferedDuplexPipe? _bufferedTransport;
    private readonly ILogger? logger;
    private readonly object _outboundGate = new();
    private readonly Channel<OutboundFrame> _outboundFrames;
    private readonly Queue<PendingFrame> _pendingFrames = [];
    private readonly object _sendPumpGate = new();
    private readonly int _maxPendingSendBytes;
    private readonly int _maxPendingSendFrames;
    private readonly OutboundBufferAdmissionGate? _outboundAdmissionGate;

    private int _backpressureStreak;
    private int _cleanupStarted;
    private int _closed;
    private int _disconnectReason = NoDisconnectReason;
    private long _pendingSendBytes;
    private int _pendingSendFrameCount;
    private bool _sendPumpRunning;
    private Task? _sendPumpTask;

    protected ClientSession(
        long sessionId,
        IDuplexPipe transport,
        FenrirServer server,
        IPEndPoint? remoteEndPoint = null,
        ILogger? logger = null,
        OutboundBufferAdmissionGate? outboundAdmissionGate = null)
    {
        ArgumentNullException.ThrowIfNull(transport);

        Transport = transport;
        Server = server;
        RemoteEndPoint = remoteEndPoint;
        this.logger = logger;
        _outboundAdmissionGate = outboundAdmissionGate;

        (_maxPendingSendBytes, _maxPendingSendFrames) = server switch
        {
            FenrirServer.Zone => (ZoneMaxPendingSendBytes, ZoneMaxPendingSendFrames),
            FenrirServer.Login => (LoginMaxPendingSendBytes, LoginMaxPendingSendFrames),
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
        };

        _outboundFrames = Channel.CreateBounded<OutboundFrame>(new BoundedChannelOptions(_maxPendingSendFrames)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _bufferedTransport = transport as IBufferedDuplexPipe;
        if (_bufferedTransport is not null)
            _bufferedTransport.OutputBytesConsumed += OnOutputBytesConsumed;

        SessionId = sessionId;
        LastActivityUtc = DateTimeOffset.UtcNow;
    }

    private IDuplexPipe Transport { get; }

    internal PipeReader Input => Transport.Input;

    public FenrirServer Server { get; }

    public byte InboundStreamXorKey { get; set; }

    public DisconnectReason? DisconnectReason
    {
        get
        {
            var value = Volatile.Read(ref _disconnectReason);
            return value == NoDisconnectReason ? null : (DisconnectReason)value;
        }
    }

    public IPEndPoint? RemoteEndPoint { get; }

    public DateTimeOffset LastActivityUtc { get; private set; }

    public long SessionId { get; }

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        try
        {
            QueuePacket(in packet);
        }
        catch
        {
            Abort(Core.Abstractions.DisconnectReason.Faulted);
            throw;
        }
    }

    public void SendRaw(ReadOnlySpan<byte> rawFrame)
    {
        try
        {
            QueueRawFrame(rawFrame);
        }
        catch
        {
            Abort(Core.Abstractions.DisconnectReason.Faulted);
            throw;
        }
    }

    public void Abort(DisconnectReason reason)
    {
        if (Interlocked.CompareExchange(ref _disconnectReason, (int)reason, NoDisconnectReason) != NoDisconnectReason)
            return;

        NetworkSessionMetrics.SessionTerminations.Add(1, NetworkSessionMetrics.ServerTag(Server),
            NetworkSessionMetrics.ReasonTag(reason));
        Volatile.Write(ref _closed, 1);
        CloseOutboundQueue();

        Transport.Input.CancelPendingRead();
        Transport.Output.CancelPendingFlush();
        StartSendPump();
    }

    public void Touch()
    {
        LastActivityUtc = DateTimeOffset.UtcNow;
    }

    public abstract bool IsOpcodeAllowed(byte opcode);

    public virtual bool ShouldWithholdOpcode(byte opcode)
    {
        return false;
    }

    protected void LogSessionStateChanged<TState>(TState previousState, TState newState) where TState : struct, Enum
    {
        logger?.LogInformation(
            "Session {SessionId} ({Server}) state changed: {PreviousState} -> {NewState}",
            SessionId, Server, previousState, newState);
    }

    public async ValueTask CompleteAsync()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
            return;

        Volatile.Write(ref _closed, 1);
        CloseOutboundQueue();
        Transport.Input.CancelPendingRead();
        Transport.Output.CancelPendingFlush();
        StartSendPump();

        try
        {
            await Transport.Input.CompleteAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }

        if (!await WaitForSendPumpAsync().ConfigureAwait(false))
        {
            logger?.LogWarning(
                "Session {SessionId} ({Server}, {RemoteEndPoint}): send pump still active after {TimeoutMs} ms; transport output completion is deferred to connection teardown",
                SessionId, Server, RemoteEndPoint, SendPumpDrainTimeoutMs);
            DetachBufferedTransport();
            return;
        }

        try
        {
            await Transport.Output.CompleteAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            DetachBufferedTransport();
        }
    }

    private void QueuePacket<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var queued = false;
        var overflowed = false;
        var queuedDepth = 0;
        byte opcode;
        int byteSize;

        lock (_outboundGate)
        {
            if (Volatile.Read(ref _closed) != 0)
                return;

            var frame = TPacket.Compressed
                ? CompressedFrameWriter.WriteCompressedFrame(in packet)
                : WriteFrame(in packet);
            opcode = OpcodeOf(frame);
            byteSize = frame.Length;
            queued = TryQueueFrameUnsafe(frame, out overflowed, out queuedDepth);
        }

        CompleteQueueAttempt(opcode, byteSize, queued, overflowed, queuedDepth);
    }

    private void QueueRawFrame(ReadOnlySpan<byte> rawFrame)
    {
        var queued = false;
        var overflowed = false;
        var queuedDepth = 0;
        byte opcode;
        int byteSize;

        lock (_outboundGate)
        {
            if (Volatile.Read(ref _closed) != 0)
                return;

            var frame = GC.AllocateUninitializedArray<byte>(rawFrame.Length);
            rawFrame.CopyTo(frame);
            opcode = OpcodeOf(frame);
            byteSize = frame.Length;
            queued = TryQueueFrameUnsafe(frame, out overflowed, out queuedDepth);
        }

        CompleteQueueAttempt(opcode, byteSize, queued, overflowed, queuedDepth);
    }

    private byte[] WriteFrame<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        var frame = GC.AllocateUninitializedArray<byte>(FrameWriter.FrameSizeOf<TPacket>());
        FrameWriter.WriteFrame(in packet, frame);
        return frame;
    }

    private bool TryQueueFrameUnsafe(byte[] frame, out bool overflowed, out int queuedDepth)
    {
        queuedDepth = 0;

        if (frame.Length > _maxPendingSendBytes ||
            _pendingSendFrameCount >= _maxPendingSendFrames ||
            _pendingSendBytes > _maxPendingSendBytes - frame.Length)
        {
            overflowed = true;
            return false;
        }

        if (_outboundAdmissionGate is not null && !_outboundAdmissionGate.TryReserve(frame.Length))
        {
            overflowed = true;
            return false;
        }

        if (_outboundFrames.Writer.TryWrite(new OutboundFrame(frame, Stopwatch.GetTimestamp())))
        {
            _pendingFrames.Enqueue(new PendingFrame(frame.Length));
            _pendingSendBytes += frame.Length;
            _pendingSendFrameCount++;
            overflowed = false;
            queuedDepth = _pendingSendFrameCount;
            return true;
        }

        _outboundAdmissionGate?.Release(frame.Length, 1);
        overflowed = Volatile.Read(ref _closed) == 0;
        return false;
    }

    private void CompleteQueueAttempt(byte opcode, int byteSize, bool queued, bool overflowed, int queuedDepth)
    {
        if (overflowed)
        {
            NetworkSessionMetrics.OutboundQueueRejections.Add(1, NetworkSessionMetrics.ServerTag(Server));
            AbortForPendingSendOverflow(opcode);
            return;
        }

        if (!queued)
            return;

        NetworkSessionMetrics.OutboundQueueDepth.Record(queuedDepth, NetworkSessionMetrics.ServerTag(Server));
        LogPacketSent(opcode, byteSize);
        StartSendPump();
    }

    private void AbortForPendingSendOverflow(byte opcode)
    {
        logger?.LogWarning(
            "Session {SessionId} ({Server}, {RemoteEndPoint}): aborting -- pending outbound frames or bytes for opcode {Opcode} exceed a session or process admission cap ({MaxPendingSendFrames} frames, {MaxPendingSendBytes} bytes per session)",
            SessionId, Server, RemoteEndPoint, opcode, _maxPendingSendFrames, _maxPendingSendBytes);
        Abort(Core.Abstractions.DisconnectReason.SendBufferOverflow);
    }

    private async Task DrainOutboundFramesAsync()
    {
        await Task.Yield();

        try
        {
            while (_outboundFrames.Reader.TryRead(out var frame))
            {
                if (Volatile.Read(ref _closed) != 0)
                    continue;

                try
                {
                    var span = Transport.Output.GetSpan(frame.Bytes.Length);
                    frame.Bytes.CopyTo(span);
                    Transport.Output.Advance(frame.Bytes.Length);

                    if (!await FlushFrameAsync().ConfigureAwait(false))
                        return;

                    NetworkSessionMetrics.OutboundBytes.Add(frame.Bytes.Length, NetworkSessionMetrics.ServerTag(Server));
                    NetworkSessionMetrics.OutboundQueueAgeMs.Record(
                        Stopwatch.GetElapsedTime(frame.EnqueuedAtTimestamp).TotalMilliseconds,
                        NetworkSessionMetrics.ServerTag(Server));

                    if (_bufferedTransport is null)
                        ReleaseSentBytes(frame.Bytes.Length);
                }
                catch (OperationCanceledException) when (Volatile.Read(ref _closed) != 0)
                {
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException || Volatile.Read(ref _closed) == 0)
                {
                    logger?.LogDebug(ex,
                        "Session {SessionId} ({Server}, {RemoteEndPoint}): outbound frame write failed; aborting",
                        SessionId, Server, RemoteEndPoint);
                    Abort(Core.Abstractions.DisconnectReason.Faulted);
                    return;
                }
            }
        }
        finally
        {
            lock (_sendPumpGate)
                _sendPumpRunning = false;

            if (_outboundFrames.Reader.TryPeek(out _))
                StartSendPump();
        }
    }

    private async ValueTask<bool> FlushFrameAsync()
    {
        var flush = Transport.Output.FlushAsync();
        var completedSynchronously = flush.IsCompletedSuccessfully;
        var result = await flush.ConfigureAwait(false);

        if (result is { IsCanceled: true } or { IsCompleted: true })
        {
            if (Volatile.Read(ref _closed) == 0)
                Abort(Core.Abstractions.DisconnectReason.Faulted);

            return false;
        }

        if (completedSynchronously)
        {
            _backpressureStreak = 0;
            return true;
        }

        if (++_backpressureStreak < SlowConsumerBackpressureStreakLimit)
            return true;

        logger?.LogWarning(
            "Session {SessionId}: aborting as a slow consumer -- {Streak} consecutive non-synchronous TX flushes",
            SessionId, _backpressureStreak);
        Abort(Core.Abstractions.DisconnectReason.SlowConsumer);
        return false;
    }

    private void StartSendPump()
    {
        lock (_sendPumpGate)
        {
            if (_sendPumpRunning)
                return;

            _sendPumpRunning = true;
            _sendPumpTask = DrainOutboundFramesAsync();
        }
    }

    private void OnOutputBytesConsumed(long bytes)
    {
        lock (_outboundGate)
        {
            if (Volatile.Read(ref _closed) != 0)
                return;

            ReleaseSentBytesUnsafe(bytes);
        }
    }

    private void ReleaseSentBytes(long bytes)
    {
        lock (_outboundGate)
            ReleaseSentBytesUnsafe(bytes);
    }

    private void ReleaseSentBytesUnsafe(long bytes)
    {
        var releasedBytes = 0L;
        var releasedFrames = 0;

        while (_pendingFrames.TryPeek(out var frame))
        {
            if (frame.RemainingBytes == 0)
            {
                _pendingFrames.Dequeue();
                _pendingSendFrameCount--;
                releasedFrames++;
                continue;
            }

            if (bytes <= 0)
                break;

            var consumed = (int)Math.Min(bytes, frame.RemainingBytes);
            frame.RemainingBytes -= consumed;
            _pendingSendBytes -= consumed;
            bytes -= consumed;
            releasedBytes += consumed;

            if (frame.RemainingBytes != 0)
                continue;

            _pendingFrames.Dequeue();
            _pendingSendFrameCount--;
            releasedFrames++;
        }

        _outboundAdmissionGate?.Release((int)releasedBytes, releasedFrames);
    }

    private void CloseOutboundQueue()
    {
        lock (_outboundGate)
        {
            var pendingBytes = _pendingSendBytes;
            var pendingFrames = _pendingSendFrameCount;
            _outboundFrames.Writer.TryComplete();
            _pendingFrames.Clear();
            _pendingSendBytes = 0;
            _pendingSendFrameCount = 0;
            _outboundAdmissionGate?.Release((int)pendingBytes, pendingFrames);
        }
    }

    private async ValueTask<bool> WaitForSendPumpAsync()
    {
        Task? task;
        lock (_sendPumpGate)
            task = _sendPumpTask;

        if (task is null || task.IsCompleted)
            return true;

        return await Task.WhenAny(task, Task.Delay(SendPumpDrainTimeoutMs)).ConfigureAwait(false) == task;
    }

    private void DetachBufferedTransport()
    {
        if (_bufferedTransport is not null)
            _bufferedTransport.OutputBytesConsumed -= OnOutputBytesConsumed;
    }

    private void LogPacketSent(byte opcode, int byteSize)
    {
        logger?.PacketSent(SessionId, opcode, byteSize);
    }

    private static byte OpcodeOf(ReadOnlySpan<byte> rawFrame)
    {
        return rawFrame.IsEmpty ? (byte)0 : rawFrame[0];
    }

    private readonly record struct OutboundFrame(byte[] Bytes, long EnqueuedAtTimestamp);

    private sealed class PendingFrame(int byteLength)
    {
        public int RemainingBytes { get; set; } = byteLength;
    }
}
