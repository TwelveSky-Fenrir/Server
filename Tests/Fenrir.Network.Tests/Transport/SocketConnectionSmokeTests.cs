using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Dispatch.Login.Sessions;
using Fenrir.Network.Transport;

namespace Fenrir.Network.Tests.Transport;

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

        var listener = new FenrirTcpListener<LoginClientSession>(
            new IPEndPoint(IPAddress.Loopback, port),
            static (sessionId, transport, remoteEndPoint) =>
                new LoginClientSession(sessionId, transport, remoteEndPoint));

        var acceptLoop = listener.AcceptLoopAsync(
            (_, connection, acceptCt) =>
            {
                accepted.TrySetResult(connection);
                return connection.RunIoAsync(acceptCt);
            },
            ct);

        using var client = new TcpClient();
        SocketConnection? server = null;

        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, ct);
            server = await accepted.Task.WaitAsync(ct);

            byte[] toServer = [0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03];
            await client.GetStream().WriteAsync(toServer, ct);

            var readResult = await server.Input.ReadAtLeastAsync(toServer.Length, ct);
            Assert.Equal(toServer, readResult.Buffer.Slice(0, toServer.Length).ToArray());
            server.Input.AdvanceTo(readResult.Buffer.GetPosition(toServer.Length));

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
                Assert.True(read > 0);
                totalRead += read;
            }

            Assert.Equal(toClient, receiveBuffer);
        }
        finally
        {
            await cts.CancelAsync();
            if (server is not null)
                await server.DisposeAsync();
            await listener.DisposeAsync();
            await Swallow(acceptLoop);
        }
    }

    private static int ReserveEphemeralLoopbackPort()
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.LocalEndPoint!).Port;
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
