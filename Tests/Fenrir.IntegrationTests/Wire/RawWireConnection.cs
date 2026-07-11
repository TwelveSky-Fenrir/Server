using System.Net.Sockets;
using Fenrir.Network.Compression;

namespace Fenrir.IntegrationTests.Wire;

public sealed class RawWireConnection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private byte _outboundStreamKey;
    private NetworkStream? _stream;

    private RawWireConnection(TcpClient client)
    {
        _client = client;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
            await _stream.DisposeAsync();
        _client.Dispose();
    }

    public static async Task<RawWireConnection> ConnectAsync(int port, CancellationToken ct)
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port, ct);
        var connection = new RawWireConnection(client) { _stream = client.GetStream() };
        return connection;
    }

    public void SeedOutboundStreamKey(int randomNumber)
    {
        _outboundStreamKey = unchecked((byte)randomNumber);
    }

    public async Task SendAsync(byte[] plainFrame, CancellationToken ct)
    {
        var buffer = (byte[])plainFrame.Clone();
        WireXor.ApplyStreamXor(buffer, _outboundStreamKey);
        await _stream!.WriteAsync(buffer, ct);
    }

    public async Task<byte[]> ReadExactAsync(int length, CancellationToken ct)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await _stream!.ReadAsync(buffer.AsMemory(offset, length - offset), ct);
            if (read == 0)
                throw new IOException(
                    $"Peer closed the connection after {offset} of {length} expected bytes.");
            offset += read;
        }

        return buffer;
    }
}
