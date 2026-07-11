using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Logging;
using Fenrir.Network.Framing;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Dispatch.Sessions;

public abstract class ClientSession(
    long sessionId,
    IDuplexPipe transport,
    FenrirServer server,
    IPEndPoint? remoteEndPoint = null,
    ILogger? logger = null)
    : IPacketSession
{
    private const int SlowConsumerBackpressureStreakLimit = 5;

        private readonly Channel<byte[]> _pendingSends =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private int _backpressureStreak;
    private int _completed;

    public IDuplexPipe Transport { get; } = transport;

    public FenrirServer Server { get; } = server;

        public IPEndPoint? RemoteEndPoint { get; } = remoteEndPoint;

        public byte InboundStreamXorKey { get; set; }

    public DisconnectReason? DisconnectReason { get; private set; }

        public DateTimeOffset LastActivityUtc { get; private set; } = DateTimeOffset.UtcNow;

        public long SessionId { get; } = sessionId;

        public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        if (Volatile.Read(ref _completed) != 0)
            return;

        if (TPacket.Compressed)
        {
            SendRaw(FrameWriter.WriteCompressedFrame(in packet));
            return;
        }

        var total = FrameWriter.FrameSizeOf<TPacket>();

        if (!_sendLock.Wait(0))
        {
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

        public void Touch()
    {
        LastActivityUtc = DateTimeOffset.UtcNow;
    }

        public abstract bool IsOpcodeAllowed(byte opcode);

    public void SendRaw(ReadOnlySpan<byte> rawFrame)
    {
        if (Volatile.Read(ref _completed) != 0)
            return;

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

        private static byte OpcodeOf(ReadOnlySpan<byte> rawFrame)
    {
        return rawFrame.IsEmpty ? (byte)0 : rawFrame[0];
    }

        private void LogPacketSent(byte opcode, int byteSize)
    {
        logger?.PacketSent(SessionId, opcode, byteSize);
    }

        protected void LogSessionStateChanged<TState>(TState previousState, TState newState) where TState : struct, Enum
    {
        logger?.LogInformation(
            "Session {SessionId} ({Server}) state changed: {PreviousState} -> {NewState}",
            SessionId, Server, previousState, newState);
    }

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

                if (!_pendingSends.Reader.TryPeek(out _) || !_sendLock.Wait(0))
                    return;
            }
        }
    }

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

    private void ClaimOwnershipIfNowFreeToAvoidStrandedFrame()
    {
        if (_sendLock.Wait(0))
            FlushLocked();
    }

    private async ValueTask ObserveFlushAsync(ValueTask<FlushResult> flush)
    {
        bool keepDraining;

        try
        {
            var result = await flush.ConfigureAwait(false);
            keepDraining = result is { IsCanceled: false, IsCompleted: false };

            if (keepDraining && ++_backpressureStreak >= SlowConsumerBackpressureStreakLimit)
            {
                logger?.LogWarning(
                    "Session {SessionId}: aborting as a slow consumer -- {Streak} consecutive non-synchronous TX flushes",
                    SessionId, _backpressureStreak);
                Abort(Sessions.DisconnectReason.SlowConsumer);
                keepDraining = false;
            }
        }
        catch (Exception)
        {
            keepDraining = false;
        }

        if (!keepDraining)
        {
            _sendLock.Release();
            return;
        }

        try
        {
            if (TryWriteNextQueuedFrame())
                FlushLocked();
            else
                _sendLock.Release();
        }
        catch
        {
        }
    }

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
