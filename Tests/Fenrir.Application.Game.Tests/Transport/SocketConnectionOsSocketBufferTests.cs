using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Transport;

namespace Fenrir.Application.Game.Tests.Transport;

public sealed class SocketConnectionOsSocketBufferTests
{
    private const int ExpectedSendBufferSize = 204800;

    [Fact]
    public async Task Constructor_ApplyOsSocketBuffersTrue_RaisesSendBufferAndSetsReceiveBuffer()
    {
        Socket? client = null;
        Socket? listener = null;
        try
        {
            var pair = CreateConnectedServerSocket();
            client = pair.Client;
            listener = pair.Listener;
            var server = pair.Server;

            var defaultReceive = server.ReceiveBufferSize;

            await using var connection = new SocketConnection(server, null, true);

            Assert.True(server.SendBufferSize >= ExpectedSendBufferSize,
                $"SO_SNDBUF should be >= {ExpectedSendBufferSize}, was {server.SendBufferSize}");
            Assert.NotEqual(defaultReceive, server.ReceiveBufferSize);
        }
        finally
        {
            client?.Dispose();
            listener?.Dispose();
        }
    }

    [Fact]
    public async Task Constructor_ApplyOsSocketBuffersFalse_LeavesBuffersAtOsDefault()
    {
        Socket? client = null;
        Socket? listener = null;
        try
        {
            var pair = CreateConnectedServerSocket();
            client = pair.Client;
            listener = pair.Listener;
            var server = pair.Server;

            var defaultSend = server.SendBufferSize;
            var defaultReceive = server.ReceiveBufferSize;

            await using var connection = new SocketConnection(server);

            Assert.Equal(defaultSend, server.SendBufferSize);
            Assert.Equal(defaultReceive, server.ReceiveBufferSize);
        }
        finally
        {
            client?.Dispose();
            listener?.Dispose();
        }
    }

    [Fact]
    public async Task Constructor_DefaultFlag_LeavesBuffersAtOsDefault()
    {
        Socket? client = null;
        Socket? listener = null;
        try
        {
            var pair = CreateConnectedServerSocket();
            client = pair.Client;
            listener = pair.Listener;
            var server = pair.Server;

            var defaultSend = server.SendBufferSize;
            var defaultReceive = server.ReceiveBufferSize;

            await using var connection = new SocketConnection(server);

            Assert.Equal(defaultSend, server.SendBufferSize);
            Assert.Equal(defaultReceive, server.ReceiveBufferSize);
        }
        finally
        {
            client?.Dispose();
            listener?.Dispose();
        }
    }

    private static (Socket Server, Socket Client, Socket Listener) CreateConnectedServerSocket()
    {
        var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndPoint!);

        var server = listener.Accept();
        return (server, client, listener);
    }
}
