using System.Buffers;
using System.IO.Pipelines;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Tests.Sessions;

/// <summary>Confirms <c>ClientSession</c>'s two send paths put the exact expected bytes on the wire.</summary>
public class ClientSessionSendTests
{
    // ZoneGreetingResponse declares no Obfuscation, so FrameWriter takes the no-XOR path.
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

    // SendRaw is the pre-built-frame path (LZ4/ZPACKET) -- it must never reinterpret the caller's bytes.
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

    // Regression test for the GetSpan/Advance/FlushAsync race: PipeWriter forbids concurrent use, so many
    // threads hammering Send() on one session must still serialize into exactly the expected byte stream.
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

    // Drives a real (tiny-threshold) Pipe whose reader only ever drains what's asked, so every flush hits
    // backpressure; once that persists across consecutive sends the session must self-disconnect rather than
    // silently stall forever.
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

        // Send never blocks the caller anymore, even while a previous send on this same session is still
        // draining under backpressure (see ClientSession's own _pendingSends remarks) -- every one of these 12
        // calls returns immediately regardless of how far behind the pipe is, queuing instead of waiting. The
        // five-streak eviction itself only completes once each queued frame's own flush has actually resolved
        // against the reader above, which happens on a background continuation rather than inline here, so
        // this polls for it instead of assuming the send loop itself drives the eviction to completion.
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
}
