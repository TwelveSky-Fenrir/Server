using System.Buffers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Transport;

// Workstream D3 / Contract A (Server/ts25zone/S04_MyWork01.cpp:283-286, :389): the drain loop must re-check the
// session's disconnect state between buffered frames, so once a handler aborts/quits the session no further
// already-buffered frame from the same receive is dispatched. Exercises SessionLoop.RunAsync against a real Pipe
// pair (via ZoneTestKit's FakeDuplexPipe), writing several complete frames into one buffer.
public sealed class SessionLoopMidDrainDisconnectTests
{
    private static readonly TimeSpan LoopTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_TwoFramesInOneBuffer_NoHandlerAbort_DispatchesBoth()
    {
        var (session, pipe) = InWorldSession(1);
        var dispatcher = new RecordingAbortingDispatcher(); // never aborts

        // Both complete frames written and the writer completed BEFORE the loop starts, so the first ReadAsync
        // returns one buffer containing both -- ProcessBufferAsync drains them in a single call.
        await pipe.PeerToSession.WriteAsync(TwoFrames());
        await pipe.PeerToSession.CompleteAsync();

        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);
        await AwaitLoopAsync(loopTask);

        // Baseline: without any abort, a multi-frame buffer is fully drained -- proves the new re-check does not
        // over-eagerly stop a healthy session mid-buffer.
        Assert.Equal(2, dispatcher.DispatchCount);
        Assert.Equal(DisconnectReason.ClientClosed, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_TwoFramesInOneBuffer_FirstHandlerAborts_SecondFrameNeverDispatched()
    {
        var (session, pipe) = InWorldSession(2);
        // The first handler kicks the session (a GM KICK is the canonical mid-buffer, handler-initiated
        // Quit-equivalent); the second buffered frame must NOT reach the dispatcher afterwards.
        var dispatcher = new RecordingAbortingDispatcher(abortOnCall: 1, abortReason: DisconnectReason.GmKicked);

        await pipe.PeerToSession.WriteAsync(TwoFrames());
        await pipe.PeerToSession.CompleteAsync();

        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);
        await AwaitLoopAsync(loopTask);

        Assert.Equal(1, dispatcher.DispatchCount);
        // Abort is idempotent -- the handler's own reason sticks, never overwritten by any later loop bookkeeping.
        Assert.Equal(DisconnectReason.GmKicked, session.DisconnectReason);
    }

    private static (ZoneClientSession Session, FakeDuplexPipe Pipe) InWorldSession(long sessionId)
    {
        var (session, pipe) = ZoneTestKit.CreateSession(sessionId);
        session.MarkTicketConsumed(1, 1);
        session.MarkRegistering();
        session.MarkInWorld();
        return (session, pipe);
    }

    // Two back-to-back HeartbeatRequest frames (allowed in InWorld) concatenated into one contiguous buffer.
    private static byte[] TwoFrames()
    {
        var one = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        var both = new byte[one.Length * 2];
        one.CopyTo(both, 0);
        one.CopyTo(both, one.Length);
        return both;
    }

    // Raw CLIENT_PACKET frame (9-byte header, opcode at offset 8); payload content is never parsed here.
    private static byte[] BuildClientFrame(byte opcode, int payloadSize)
    {
        var frame = new byte[WireHeaderSizes.ClientPacketSize + payloadSize];
        frame[8] = opcode;
        return frame;
    }

    private static async Task AwaitLoopAsync(Task loopTask)
    {
        var completed = await Task.WhenAny(loopTask, Task.Delay(LoopTimeout));
        Assert.Same(loopTask, completed);
        await loopTask;
    }

    // Records each dispatch and, on the configured 1-based call number, aborts the session with the given reason
    // (mirroring a handler that calls session.Abort/Quit mid-buffer). abortOnCall <= 0 means "never abort".
    private sealed class RecordingAbortingDispatcher(
        int abortOnCall = 0,
        DisconnectReason abortReason = DisconnectReason.GmKicked) : IFrameDispatcher
    {
        public int DispatchCount { get; private set; }

        public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
            IPacketSession session, CancellationToken cancellationToken)
        {
            DispatchCount++;

            if (DispatchCount == abortOnCall && session is ClientSession clientSession)
                clientSession.Abort(abortReason);

            return ValueTask.CompletedTask;
        }
    }
}
