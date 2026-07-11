using System.Buffers;
using System.IO.Pipelines;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Tests.Sessions;

public class ClientSessionSendTests
{
    [Fact]
    public async Task Send_WritesOpcodeAndPayload_ForPacketWithoutObfuscation()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        var packet = new ZoneGreetingResponse { RandomNumber = 0x12345678 };

        session.Send(packet);

        var result = await pipe.SessionToPeer.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);

        Assert.Equal(new byte[] { 0x00, 0x78, 0x56, 0x34, 0x12 }, bytes);
    }

    [Fact]
    public async Task SendRaw_WritesSuppliedBytesUnchanged()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        byte[] raw = [0xDE, 0xAD, 0xBE, 0xEF, 0x01];

        session.SendRaw(raw);

        var result = await pipe.SessionToPeer.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);

        Assert.Equal(raw, bytes);
    }

    [Fact]
    public async Task Send_FromManyConcurrentCallers_NeverCorruptsTheStreamOrThrows()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        var packet = new ZoneGreetingResponse { RandomNumber = 0x12345678 };
        byte[] expectedFrame = [0x00, 0x78, 0x56, 0x34, 0x12];

        const int callers = 8;
        const int sendsPerCaller = 500;
        var expectedTotalBytes = callers * sendsPerCaller * expectedFrame.Length;

        var callerTasks = Enumerable.Range(0, callers)
            .Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < sendsPerCaller; i++)
                    session.Send(packet);
            }))
            .ToArray();

        await Task.WhenAll(callerTasks);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new List<byte>(expectedTotalBytes);
        while (received.Count < expectedTotalBytes)
        {
            var result = await pipe.SessionToPeer.ReadAsync(cts.Token);
            received.AddRange(result.Buffer.ToArray());
            pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
        }

        Assert.Equal(expectedTotalBytes, received.Count);
        for (var offset = 0; offset < received.Count; offset += expectedFrame.Length)
            Assert.Equal(expectedFrame, received.GetRange(offset, expectedFrame.Length));
        Assert.Null(session.DisconnectReason);
    }

    [Fact]
    public async Task Send_DisconnectsAsSlowConsumer_WhenBackpressurePersistsAcrossConsecutiveSends()
    {
        var outboundOptions = new PipeOptions(
            pauseWriterThreshold: 1,
            resumeWriterThreshold: 0,
            useSynchronizationContext: false);
        var pipe = new FakeDuplexPipe(outboundOptions);
        var session = new ZoneClientSession(1, pipe);
        var packet = new ZoneGreetingResponse { RandomNumber = 1 };

        using var readerCts = new CancellationTokenSource();
        var readerTask = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var result = await pipe.SessionToPeer.ReadAsync(readerCts.Token);
                    pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
                    if (result.IsCompleted || result.IsCanceled)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        for (var i = 0; i < 12; i++)
            session.Send(packet);

        using var deadlineCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (session.DisconnectReason is null && !deadlineCts.IsCancellationRequested)
            await Task.Delay(10, CancellationToken.None);

        readerCts.Cancel();
        await Swallow(readerTask);

        Assert.Equal(DisconnectReason.SlowConsumer, session.DisconnectReason);
    }

    private static async Task Swallow(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public async Task Send_LogsPacketSentAtDebug_WhenDebugEnabled()
    {
        var pipe = new FakeDuplexPipe();
        var logger = new CapturingLogger(LogLevel.Debug);
        var session = new ZoneClientSession(42, pipe, logger: logger);
        var packet = new ZoneGreetingResponse { RandomNumber = 0x12345678 };

        session.Send(packet);

        await DrainAsync(pipe);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("42", entry.Message);
        Assert.Contains(ZoneGreetingResponse.Opcode.ToString(), entry.Message);
    }

    [Fact]
    public async Task Send_LogsNothing_WhenDebugDisabled()
    {
        var pipe = new FakeDuplexPipe();
        var logger = new CapturingLogger(LogLevel.Information);
        var session = new ZoneClientSession(1, pipe, logger: logger);
        var packet = new ZoneGreetingResponse { RandomNumber = 1 };

        session.Send(packet);

        await DrainAsync(pipe);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task SendRaw_LogsPacketSentWithOpcodeFromFirstByte_WhenDebugEnabled()
    {
        var pipe = new FakeDuplexPipe();
        var logger = new CapturingLogger(LogLevel.Debug);
        var session = new ZoneClientSession(7, pipe, logger: logger);
        byte[] raw = [0xAB, 0xBE, 0xEF];

        session.SendRaw(raw);

        await DrainAsync(pipe);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("7", entry.Message);
        Assert.Contains("171", entry.Message);
    }

    [Fact]
    public async Task Send_NeverThrows_WhenNoLoggerWired()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        var packet = new ZoneGreetingResponse { RandomNumber = 1 };

        session.Send(packet);

        await DrainAsync(pipe);
    }

    [Fact]
    public async Task Send_AfterAbortAndComplete_IsSilentNoOp()
    {
        var pipe = new FakeDuplexPipe();
        var session = new ZoneClientSession(1, pipe);
        var packet = new ZoneGreetingResponse { RandomNumber = 1 };

        session.Abort(DisconnectReason.Faulted);
        await session.CompleteAsync();

        session.Send(packet);
        session.SendRaw([0xAB, 0xBE, 0xEF]);

        Assert.True(pipe.SessionToPeer.TryRead(out var result));
        Assert.True(result.IsCompleted);
        Assert.True(result.Buffer.IsEmpty);
    }

    private static async Task DrainAsync(FakeDuplexPipe pipe)
    {
        var result = await pipe.SessionToPeer.ReadAsync();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
    }
}
