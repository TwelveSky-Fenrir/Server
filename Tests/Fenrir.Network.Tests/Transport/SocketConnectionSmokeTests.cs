using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Sessions;
using Fenrir.Network.Transport;

namespace Fenrir.Network.Tests.Transport;

/// <summary>
///     One end-to-end pass over a real loopback TCP socket. FrameDecoder/SessionLoop already exercise framing and
///     fragmentation exhaustively against in-memory pipes, so this only needs to prove the wiring underneath them:
///     <see cref="FenrirTcpListener" /> actually accepts a live connection, and raw bytes cross the resulting
///     <see cref="SocketConnection" />'s Input/Output pipes unchanged in both directions.
/// </summary>
public sealed class SocketConnectionSmokeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task AcceptedConnection_RoundTripsRawBytesUnchangedInBothDirections()
    {
        using var cts = new CancellationTokenSource(TestTimeout);
        var ct = cts.Token;

        var port = ReserveEphemeralLoopbackPort();
        var accepted = new TaskCompletionSource<SocketConnection>(TaskCreationOptions.RunContinuationsAsynchronously);

        var listener = new FenrirTcpListener(
            new IPEndPoint(IPAddress.Loopback, port),
            static (sessionId, transport, remoteEndPoint) =>
                new LoginClientSession(sessionId, transport, remoteEndPoint));

        // Detached by design (FenrirTcpListener.AcceptLoopAsync's own contract): drive it in the background and
        // only reconcile with it in the finally block below.
        var acceptLoop = listener.AcceptLoopAsync(
            (_, connection, acceptCt) =>
            {
                accepted.TrySetResult(connection);
                return connection.RunIOAsync(acceptCt); // keeps pumping the socket for the rest of this test
            },
            ct);

        using var client = new TcpClient();
        SocketConnection? server = null;

        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, ct);
            server = await accepted.Task.WaitAsync(ct);

            // Client -> server: default GetInboundXorKey is "() => 0", a no-op per WireXor.ApplyStreamXor, so
            // the bytes reaching SocketConnection.Input must be bit-for-bit what the client wrote.
            byte[] toServer = [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03];
            await client.GetStream().WriteAsync(toServer, ct);

            var readResult = await server.Input.ReadAtLeastAsync(toServer.Length, ct);
            Assert.Equal(toServer, readResult.Buffer.Slice(0, toServer.Length).ToArray());
            server.Input.AdvanceTo(readResult.Buffer.GetPosition(toServer.Length));

            // Server -> client: the send loop never touches the bytes (no XOR on the way out), so this must
            // arrive at the client exactly as written to SocketConnection.Output.
            byte[] toClient = [0xFE, 0xED, 0xFA, 0xCE, 0x99];
            var destination = server.Output.GetSpan(toClient.Length);
            toClient.CopyTo(destination);
            server.Output.Advance(toClient.Length);
            await server.Output.FlushAsync(ct);

            var receiveBuffer = new byte[toClient.Length];
            var totalRead = 0;
            while (totalRead < receiveBuffer.Length)
            {
                var read = await client.GetStream().ReadAsync(receiveBuffer.AsMemory(totalRead), ct);
                Assert.True(read > 0); // 0 would mean the peer closed early, mid round trip
                totalRead += read;
            }

            Assert.Equal(toClient, receiveBuffer);
        }
        finally
        {
            await cts.CancelAsync(); // unblocks the receive/send loops still parked in Socket.*Async
            if (server is not null)
                await server.DisposeAsync();
            await listener.DisposeAsync();
            await Swallow(acceptLoop);
        }
    }

    /// <summary>
    ///     Binds to port 0 to let the OS pick a free loopback port, then releases it immediately so
    ///     <see cref="FenrirTcpListener" /> — which only ever binds the endpoint it is given, never reports back
    ///     what it bound to — can bind that same number itself.
    /// </summary>
    private static int ReserveEphemeralLoopbackPort()
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.LocalEndPoint!).Port;
    }

    /// <summary>
    ///     AcceptLoopAsync only ever exits via cancellation or listen-socket disposal, both of which it already swallows
    ///     internally, but this guards the test against that contract changing underneath it.
    /// </summary>
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
