using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Dispatching;
using Fenrir.Network.Sessions;
using Fenrir.Network.Tests.Sessions;
using Fenrir.Network.Tests.TestSupport;

namespace Fenrir.Network.Tests.Dispatching;

/// <summary>
///     Integration-level coverage of <see cref="SessionLoop" />: the full read → decode → state-gate →
///     rate-limit → dispatch pipeline against a real <see cref="System.IO.Pipelines.Pipe" /> pair
///     (<see cref="FakeDuplexPipe" />), not just <see cref="Fenrir.Network.Framing.FrameDecoder" /> in isolation.
///     All scenarios use <see cref="ZoneClientSession" /> since the Zone opcodes (§4.9) already used elsewhere in
///     this suite exercise both a state-gated and an ungated case.
/// </summary>
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
        await pipe.PeerToSession.CompleteAsync(); // closes the client side so the loop terminates deterministically

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
            await pipe.PeerToSession.WriteAsync(frame.AsMemory(i,
                1)); // WriteAsync flushes -> one real, separate segment per byte

            if (i < frame.Length - 1)
            {
                await Task.Yield();
                Assert.Empty(dispatcher.Records); // decode cannot succeed on a partial frame regardless of scheduling
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

        // EnterWorld is only allowed once TicketConsumed (§8.1) -> illegal here.
        var frame = BuildClientFrame(EnterWorldRequest.Opcode, EnterWorldRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);

        await AwaitLoopAsync(loopTask); // session.Abort() ends the current iteration itself, no client-close needed

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

        // Opcode 250 is registered for neither server/direction (see Opcodes.cs) -> FrameDecoder's
        // ProtocolViolationException must be swallowed inside SessionLoop, never escape RunAsync.
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

        await pipe.PeerToSession.CompleteAsync(); // simulates the client closing the socket

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

    /// <summary>
    ///     Builds a raw <c>CLIENT_PACKET</c> frame (9-byte header, opcode at offset 8) with a distinct, checkable payload
    ///     pattern; content is never parsed by SessionLoop, only sliced.
    /// </summary>
    private static byte[] BuildClientFrame(byte opcode, int payloadSize, byte payloadSeed = 1)
    {
        var frame = new byte[WireHeaderSizes.ClientPacketSize + payloadSize];
        frame[8] = opcode;

        for (var i = 0; i < payloadSize; i++)
            frame[WireHeaderSizes.ClientPacketSize + i] = unchecked((byte)(payloadSeed + i));

        return frame;
    }

    /// <summary>
    ///     Awaits with a hard timeout so a regression that hangs RunAsync fails fast with a clear message instead of
    ///     blocking the run indefinitely.
    /// </summary>
    private static async Task AwaitLoopAsync(Task loopTask)
    {
        var completed = await Task.WhenAny(loopTask, Task.Delay(LoopTimeout));
        Assert.Same(loopTask, completed);
        await loopTask; // rethrows if RunAsync itself faulted, which the "no exception escapes" scenarios must catch
    }
}
