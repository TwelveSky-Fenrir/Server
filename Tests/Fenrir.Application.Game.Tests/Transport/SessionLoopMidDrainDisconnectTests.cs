using System.Buffers;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Tests.Transport;

public sealed class SessionLoopMidDrainDisconnectTests
{
    private static readonly TimeSpan LoopTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_TwoFramesInOneBuffer_NoHandlerAbort_DispatchesBoth()
    {
        var (session, pipe) = InWorldSession(1);
        var dispatcher = new RecordingAbortingDispatcher();

        await pipe.PeerToSession.WriteAsync(TwoFrames());
        await pipe.PeerToSession.CompleteAsync();

        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);
        await AwaitLoopAsync(loopTask);

        Assert.Equal(2, dispatcher.DispatchCount);
        Assert.Equal(DisconnectReason.ClientClosed, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_TwoFramesInOneBuffer_FirstHandlerAborts_SecondFrameNeverDispatched()
    {
        var (session, pipe) = InWorldSession(2);
        var dispatcher = new RecordingAbortingDispatcher(abortOnCall: 1, abortReason: DisconnectReason.GmKicked);

        await pipe.PeerToSession.WriteAsync(TwoFrames());
        await pipe.PeerToSession.CompleteAsync();

        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);
        await AwaitLoopAsync(loopTask);

        Assert.Equal(1, dispatcher.DispatchCount);
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

    private static byte[] TwoFrames()
    {
        var one = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        var both = new byte[one.Length * 2];
        one.CopyTo(both, 0);
        one.CopyTo(both, one.Length);
        return both;
    }

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
