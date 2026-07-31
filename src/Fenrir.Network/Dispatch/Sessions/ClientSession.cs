using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
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
    private const int LoginMaxPendingSendBytes = 40_960;
    private const int ZoneMaxPendingSendBytes = 1_024_000;

    private readonly int _maxPendingSendBytes = server switch
    {
        FenrirServer.Zone => ZoneMaxPendingSendBytes,
        FenrirServer.Login => LoginMaxPendingSendBytes,
        _ => throw new ArgumentOutOfRangeException(nameof(server), server, null)
    };

    private readonly Channel<byte[]> _pendingSends =
        Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _backpressureStreak;
    private int _completed;

    private long _pendingSendBytes;

    public IDuplexPipe Transport { get; } = transport;

    public FenrirServer Server { get; } = server;

    public byte InboundStreamXorKey { get; set; }

    public DisconnectReason? DisconnectReason { get; private set; }

    public IPEndPoint? RemoteEndPoint { get; } = remoteEndPoint;

    public DateTimeOffset LastActivityUtc { get; private set; } = DateTimeOffset.UtcNow;

    public long SessionId { get; } = sessionId;

    public void Send<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
    {
        if (Volatile.Read(ref _completed) != 0)
            return;

        if (TPacket.Compressed)
        {
            SendRaw(CompressedFrameWriter.WriteCompressedFrame(in packet));
            return;
        }

        var total = FrameWriter.FrameSizeOf<TPacket>();

        if (!_sendLock.Wait(0))
        {
            if (!TryReservePendingSendBytes(total))
            {
                AbortForPendingSendOverflow(TPacket.Opcode);
                return;
            }

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

    public void SendRaw(ReadOnlySpan<byte> rawFrame)
    {
        if (Volatile.Read(ref _completed) != 0)
            return;

        if (!_sendLock.Wait(0))
        {
            if (!TryReservePendingSendBytes(rawFrame.Length))
            {
                AbortForPendingSendOverflow(OpcodeOf(rawFrame));
                return;
            }

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

    public void Abort(DisconnectReason reason)
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) != 0)
            return;

        DisconnectReason = reason;
        Transport.Input.CancelPendingRead();
        Transport.Output.CancelPendingFlush();
    }

    public void Touch()
    {
        LastActivityUtc = DateTimeOffset.UtcNow;
    }

    public abstract bool IsOpcodeAllowed(byte opcode);

    private static byte OpcodeOf(ReadOnlySpan<byte> rawFrame)
    {
        return rawFrame.IsEmpty ? (byte)0 : rawFrame[0];
    }

    private void LogPacketSent(byte opcode, int byteSize)
    {
        logger?.PacketSent(SessionId, opcode, byteSize);
    }

    private bool TryReservePendingSendBytes(int frameLength)
    {
        if (Interlocked.Add(ref _pendingSendBytes, frameLength) <= _maxPendingSendBytes)
            return true;

        Interlocked.Add(ref _pendingSendBytes, -frameLength);
        return false;
    }

    private void AbortForPendingSendOverflow(byte opcode)
    {
        logger?.LogWarning(
            "Session {SessionId} ({Server}, {RemoteEndPoint}): aborting -- pending send bytes for opcode {Opcode} would exceed the {MaxPendingSendBytes}-byte cap",
            SessionId, Server, RemoteEndPoint, opcode, _maxPendingSendBytes);
        Abort(Core.Abstractions.DisconnectReason.SendBufferOverflow);
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
        finally
        {
            Interlocked.Add(ref _pendingSendBytes, -frame.Length);
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
                Abort(Core.Abstractions.DisconnectReason.SlowConsumer);
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

    public async ValueTask CompleteAsync()
    {
        await Transport.Input.CompleteAsync().ConfigureAwait(false);
        await Transport.Output.CompleteAsync().ConfigureAwait(false);
    }
}
