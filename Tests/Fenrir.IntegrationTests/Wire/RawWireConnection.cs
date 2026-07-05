using System.Net.Sockets;
using Fenrir.Network.Compression;

namespace Fenrir.IntegrationTests.Wire;

/// <summary>
///     Bare-socket transport shared by <see cref="LoginBotClient" />/<see cref="ZoneBotClient" />: every byte
///     the bot sends after the greeting must carry the legacy stream cipher (<c>mPacketEncryptionValue</c>,
///     §3.4) the server seeds via <c>LoginGreetingResponse</c>/<c>ZoneGreetingResponse.RandomNumber</c> --
///     <see cref="Fenrir.Network.Transport.SocketConnection" /> only ever decodes the INBOUND (client-&gt;server)
///     side with that key, so the client is the one that must apply it before writing. Server-&gt;client bytes
///     carry no stream cipher at all (see ZoneGreetingResponse's own doc comment).
/// </summary>
public sealed class RawWireConnection : IAsyncDisposable
{
    private readonly TcpClient _client;
    private NetworkStream? _stream;
    private byte _outboundStreamKey;

    private RawWireConnection(TcpClient client)
    {
        _client = client;
    }

    public static async Task<RawWireConnection> ConnectAsync(int port, CancellationToken ct)
    {
        var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port, ct);
        var connection = new RawWireConnection(client) { _stream = client.GetStream() };
        return connection;
    }

    /// <summary>Set once, right after decoding the greeting's RandomNumber -- every byte sent from then on is XORed with it.</summary>
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

    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
            await _stream.DisposeAsync();
        _client.Dispose();
    }
}
