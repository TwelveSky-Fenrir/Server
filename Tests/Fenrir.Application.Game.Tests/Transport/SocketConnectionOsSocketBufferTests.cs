using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Transport;

namespace Fenrir.Application.Game.Tests.Transport;

// Workstream D3 / Contract B (Server/Header/socket.h:18-41, Server/ts25zone/S02_MyServer.cpp:481): legacy applied
// SO_SNDBUF=204800 / SO_RCVBUF=20480 ONLY to zone (GameServer) accepted sockets, gated behind a "set buffers"
// flag that every other accept path and every listen socket leaves off. SocketConnection now mirrors that: the
// buffers are applied only when its applyOsSocketBuffers flag is set, and are left at the OS default otherwise.
public sealed class SocketConnectionOsSocketBufferTests
{
    // Server/Header/socket.h:33,:38 -- SO_SNDBUF is deliberately ~10x SO_RCVBUF.
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

            // Captured from the same socket before wrapping: its OS defaults, so the assertions below prove the
            // constructor -- not the OS -- changed them.
            var defaultReceive = server.ReceiveBufferSize;

            await using var connection = new SocketConnection(server, null, applyOsSocketBuffers: true);

            // >= (not ==) so the assertion survives platforms that report SO_SNDBUF back doubled; the default
            // accepted-socket SNDBUF is far below 204800, so passing this proves the buffer was raised.
            Assert.True(server.SendBufferSize >= ExpectedSendBufferSize,
                $"SO_SNDBUF should be >= {ExpectedSendBufferSize}, was {server.SendBufferSize}");
            // SO_RCVBUF was explicitly set to a smaller-than-default value, so it must have changed.
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

            await using var connection = new SocketConnection(server, null, applyOsSocketBuffers: false);

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
        // The default (no third argument) must match every non-zone accept path and every listen socket -- buffers
        // untouched (Server/Header/socket.h:16,:95).
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

    // A real, connected loopback accepted socket -- SocketConnection reads RemoteEndPoint at construction, which a
    // never-connected socket cannot satisfy. The returned server socket is owned by the SocketConnection (its
    // DisposeAsync disposes it); the caller disposes the client and listener.
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
