using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Dispatching;
using Fenrir.Network.Sessions;
using Fenrir.Network.Tests.Sessions;
using Fenrir.Network.Tests.TestSupport;

namespace Fenrir.Network.Tests.Dispatching;

// Integration-level coverage of SessionLoop: the full read -> decode -> state-gate -> rate-limit -> dispatch
// pipeline against a real Pipe pair, not just FrameDecoder in isolation.
public sealed class SessionLoopTests
{
    private static readonly TimeSpan LoopTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_SingleCompleteFrameWrittenAtOnce_DispatchesExactlyOnceWithOpcodeAndPayload()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(1, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, null, CancellationToken.None);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);
        await pipe.PeerToSession.CompleteAsync();

        await AwaitLoopAsync(loopTask);

        var records = dispatcher.Records;
        Assert.Single(records);
        Assert.Equal(FenrirServer.Zone, records[0].Server);
        Assert.Equal(HeartbeatRequest.Opcode, records[0].Opcode);
        Assert.Equal(session.SessionId, records[0].SessionId);
        Assert.True(frame.AsSpan(WireHeaderSizes.ClientPacketSize).SequenceEqual(records[0].Payload));
        Assert.Equal(DisconnectReason.ClientClosed, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_FrameDeliveredOneByteAtATime_DispatchesExactlyOnceAfterTheLastByte()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(2, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, null, CancellationToken.None);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize, 0x40);

        for (var i = 0; i < frame.Length; i++)
        {
            // WriteAsync flushes -> one real, separate segment per byte.
            await pipe.PeerToSession.WriteAsync(frame.AsMemory(i, 1));

            if (i < frame.Length - 1)
            {
                await Task.Yield();
                Assert.Empty(dispatcher.Records);
            }
        }

        await pipe.PeerToSession.CompleteAsync();
        await AwaitLoopAsync(loopTask);

        var records = dispatcher.Records;
        Assert.Single(records);
        Assert.Equal(HeartbeatRequest.Opcode, records[0].Opcode);
        Assert.True(frame.AsSpan(WireHeaderSizes.ClientPacketSize).SequenceEqual(records[0].Payload));
    }

    [Fact]
    public async Task RunAsync_OpcodeIllegalInCurrentState_AbortsWithStateViolationAndNeverDispatches()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(3, pipe); // Connected, not TicketConsumed
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, null, CancellationToken.None);

        // EnterWorld is only allowed once TicketConsumed -> illegal here.
        var frame = BuildClientFrame(EnterWorldRequest.Opcode, EnterWorldRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.StateViolation, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_UnknownOpcode_AbortsWithUnknownOpcodeAndNeverDispatchesAndLoopEndsCleanly()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(4, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, null, CancellationToken.None);

        // Opcode 250 is unregistered -> FrameDecoder's ProtocolViolationException must be swallowed here.
        var header = new byte[WireHeaderSizes.ClientPacketSize];
        header[8] = 250;
        await pipe.PeerToSession.WriteAsync(header);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.UnknownOpcode, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_RateLimiterRejects_AbortsWithRateLimitedAndNeverDispatchesThatFrame()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(5, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, new AlwaysRejectRateLimiter(), CancellationToken.None);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.RateLimited, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_ClientClosesWithoutSendingAnything_EndsWithClientClosedAndNoException()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(6, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, null, CancellationToken.None);

        await pipe.PeerToSession.CompleteAsync();

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.ClientClosed, session.DisconnectReason);
    }

    private static ZoneClientSession InWorldZoneSession(long sessionId, FakeDuplexPipe pipe)
    {
        var session = new ZoneClientSession(sessionId, pipe);
        session.MarkTicketConsumed(1, 1);
        session.MarkRegistering();
        session.MarkInWorld();
        return session;
    }

    // Raw CLIENT_PACKET frame (9-byte header, opcode at offset 8); content is never parsed by SessionLoop.
    private static byte[] BuildClientFrame(byte opcode, int payloadSize, byte payloadSeed = 1)
    {
        var frame = new byte[WireHeaderSizes.ClientPacketSize + payloadSize];
        frame[8] = opcode;

        for (var i = 0; i < payloadSize; i++)
            frame[WireHeaderSizes.ClientPacketSize + i] = unchecked((byte)(payloadSeed + i));

        return frame;
    }

    private static async Task AwaitLoopAsync(Task loopTask)
    {
        var completed = await Task.WhenAny(loopTask, Task.Delay(LoopTimeout));
        Assert.Same(loopTask, completed);
        await loopTask;
    }
}
