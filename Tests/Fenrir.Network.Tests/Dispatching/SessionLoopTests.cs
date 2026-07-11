using System.Net;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Fenrir.Network.Dispatch.FloodProtection;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Tests.Sessions;
using Fenrir.Network.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Tests.Dispatching;

public sealed class SessionLoopTests
{
    private static readonly TimeSpan LoopTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_SingleCompleteFrameWrittenAtOnce_DispatchesExactlyOnceWithOpcodeAndPayload()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(1, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);

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
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize, 0x40);

        for (var i = 0; i < frame.Length; i++)
        {
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
        var session = new ZoneClientSession(3, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);

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
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);

        var header = new byte[WireHeaderSizes.ClientPacketSize];
        header[8] = 250;
        await pipe.PeerToSession.WriteAsync(header);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.UnknownOpcode, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_UnknownOpcode_FloodGuardBelowThreshold_StillAbortsWithUnknownOpcodeAndDoesNotBlock()
    {
        var pipe = new FakeDuplexPipe();
        var remoteEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.7"), 4000);
        var session = new ZoneClientSession(7, pipe, remoteEndPoint);
        var registry = new SessionRegistry();
        registry.Register(session);
        var blockedIps = new List<string>();
        var floodGuard = new IpFloodGuard(40, 30, (ip, _) =>
        {
            blockedIps.Add(ip);
            return ValueTask.CompletedTask;
        }, registry);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, floodGuard,
            CancellationToken.None);

        var header = new byte[WireHeaderSizes.ClientPacketSize];
        header[8] = 250;
        await pipe.PeerToSession.WriteAsync(header);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.UnknownOpcode, session.DisconnectReason);
        Assert.Empty(blockedIps);
    }

    [Fact]
    public async Task RunAsync_UnknownOpcode_TripsProtocolViolationThreshold_AbortsWithIpBlockedAndPersistsBlock()
    {
        var pipe = new FakeDuplexPipe();
        var remoteEndPoint = new IPEndPoint(IPAddress.Parse("10.0.0.8"), 4000);
        var session = new ZoneClientSession(8, pipe, remoteEndPoint);
        var registry = new SessionRegistry();
        registry.Register(session);
        var blockedIps = new List<string>();
        var floodGuard = new IpFloodGuard(40, 1, (ip, _) =>
        {
            blockedIps.Add(ip);
            return ValueTask.CompletedTask;
        }, registry);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, floodGuard,
            CancellationToken.None);

        var header = new byte[WireHeaderSizes.ClientPacketSize];
        header[8] = 250;
        await pipe.PeerToSession.WriteAsync(header);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.IpBlocked, session.DisconnectReason);
        Assert.Equal(["10.0.0.8"], blockedIps);
    }

    [Fact]
    public async Task RunAsync_UnknownOpcode_NoRemoteEndPoint_FloodGuardNeverCalled()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(9, pipe);
        var registry = new SessionRegistry();
        var blockedIps = new List<string>();
        var floodGuard = new IpFloodGuard(40, 1, (ip, _) =>
        {
            blockedIps.Add(ip);
            return ValueTask.CompletedTask;
        }, registry);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, floodGuard,
            CancellationToken.None);

        var header = new byte[WireHeaderSizes.ClientPacketSize];
        header[8] = 250;
        await pipe.PeerToSession.WriteAsync(header);

        await AwaitLoopAsync(loopTask);

        Assert.Equal(DisconnectReason.UnknownOpcode, session.DisconnectReason);
        Assert.Empty(blockedIps);
    }

    [Fact]
    public async Task RunAsync_RateLimiterRejects_AbortsWithRateLimitedAndNeverDispatchesThatFrame()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(5, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask =
            SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, new AlwaysRejectRateLimiter(),
                null, CancellationToken.None);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.RateLimited, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_HandlerThrows_AbortsWithFaultedAndLoopEndsCleanlyWithoutPropagating()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(10, pipe);
        var dispatcher = new ThrowingFrameDispatcher(new InvalidOperationException("boom"));
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);

        await AwaitLoopAsync(loopTask);

        Assert.Equal(DisconnectReason.Faulted, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_ClientClosesWithoutSendingAnything_EndsWithClientClosedAndNoException()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(6, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None);

        await pipe.PeerToSession.CompleteAsync();

        await AwaitLoopAsync(loopTask);

        Assert.Empty(dispatcher.Records);
        Assert.Equal(DisconnectReason.ClientClosed, session.DisconnectReason);
    }

    [Fact]
    public async Task RunAsync_DebugLoggingEnabled_LogsPacketReceivedWithOpcodeAndSize()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(20, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var logger = new CapturingLogger(LogLevel.Debug);
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None, logger);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);
        await pipe.PeerToSession.CompleteAsync();

        await AwaitLoopAsync(loopTask);

        var debugEntries = logger.Entries.Where(e => e.Level == LogLevel.Debug).ToArray();

        var received = Assert.Single(debugEntries, e => e.Message.Contains("packet received"));
        Assert.Contains("20", received.Message);
        Assert.Contains(HeartbeatRequest.Opcode.ToString(), received.Message);
        Assert.Contains(HeartbeatRequest.PayloadSize.ToString(), received.Message);

        var dispatched = Assert.Single(debugEntries, e => e.Message.Contains("packet dispatched"));
        Assert.Contains("20", dispatched.Message);
        Assert.Contains(HeartbeatRequest.Opcode.ToString(), dispatched.Message);
    }

    [Fact]
    public async Task RunAsync_DebugLoggingDisabled_NeverLogsPacketReceived()
    {
        var pipe = new FakeDuplexPipe();
        var session = InWorldZoneSession(21, pipe);
        var dispatcher = new RecordingFrameDispatcher();
        var logger = new CapturingLogger(LogLevel.Information);
        var loopTask = SessionLoop.RunAsync(session, dispatcher, ZoneOpcodeRegistry.Provider, null, null,
            CancellationToken.None, logger);

        var frame = BuildClientFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize);
        await pipe.PeerToSession.WriteAsync(frame);
        await pipe.PeerToSession.CompleteAsync();

        await AwaitLoopAsync(loopTask);

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Debug);
        var ended = Assert.Single(logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains(DisconnectReason.ClientClosed.ToString(), ended.Message);
    }

    private static ZoneClientSession InWorldZoneSession(long sessionId, FakeDuplexPipe pipe)
    {
        var session = new ZoneClientSession(sessionId, pipe);
        session.MarkTicketConsumed(1, 1);
        session.MarkRegistering();
        session.MarkInWorld();
        return session;
    }

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
