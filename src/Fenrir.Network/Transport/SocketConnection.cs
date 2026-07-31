using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Transport.Logging;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport;

public sealed class SocketConnection : IDuplexPipe, IAsyncDisposable
{
    private const int ReceiveBufferSize = 4096;

    private const int SocketSendBufferSize = 204800;
    private const int SocketReceiveBufferSize = 20480;

    private readonly CancellationTokenSource _abortCts = new();
    private readonly ILogger? _logger;
    private readonly Pipe _rxPipe;

    private readonly Socket _socket;
    private readonly Pipe _txPipe;

    public SocketConnection(Socket socket, ILogger? logger = null, bool applyOsSocketBuffers = false)
    {
        _socket = socket;
        _socket.NoDelay = true;
        _logger = logger;

        if (applyOsSocketBuffers)
        {
            _socket.SendBufferSize = SocketSendBufferSize;
            _socket.ReceiveBufferSize = SocketReceiveBufferSize;
        }

        RemoteEndPoint = socket.RemoteEndPoint as IPEndPoint;

        _rxPipe = new Pipe(PipeOptionsFactory.Rx);
        _txPipe = new Pipe(PipeOptionsFactory.Tx);
    }

    public IPEndPoint? RemoteEndPoint { get; }

    public Func<byte> GetInboundXorKey { get; set; } = static () => 0;

    public async ValueTask DisposeAsync()
    {
        _socket.Dispose();
        _abortCts.Dispose();

        await _rxPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _rxPipe.Writer.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Reader.CompleteAsync().ConfigureAwait(false);
        await _txPipe.Writer.CompleteAsync().ConfigureAwait(false);
    }

    public PipeReader Input => _rxPipe.Reader;
    public PipeWriter Output => _txPipe.Writer;

    public async Task RunIoAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _abortCts.Token);
        await Task.WhenAll(ReceiveLoopAsync(linked.Token), SendLoopAsync(linked.Token)).ConfigureAwait(false);
    }

    public void Abort()
    {
        try
        {
            _abortCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var writer = _rxPipe.Writer;
        Exception? failure = null;

        try
        {
            while (true)
            {
                var memory = writer.GetMemory(ReceiveBufferSize);
                var bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None, cancellationToken)
                    .ConfigureAwait(false);

                if (bytesRead == 0)
                    break;

                WireXor.ApplyStreamXor(memory.Span[..bytesRead], GetInboundXorKey());
                writer.Advance(bytesRead);

                var flushResult = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                if (flushResult.IsCompleted || flushResult.IsCanceled)
                    break;
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await writer.CompleteAsync(failure).ConfigureAwait(false);
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        var reader = _txPipe.Reader;
        Exception? failure = null;

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    var remaining = segment;
                    while (!remaining.IsEmpty)
                    {
                        var sent = await _socket.SendAsync(remaining, SocketFlags.None, cancellationToken)
                            .ConfigureAwait(false);
                        remaining = remaining[sent..];
                    }
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted || result.IsCanceled)
                    break;
            }
        }
        catch (Exception ex)
        {
            failure = ex;

            if (ex is not OperationCanceledException)
                _logger?.SendLoopFaulted(ex, RemoteEndPoint);
        }
        finally
        {
            await reader.CompleteAsync(failure).ConfigureAwait(false);
        }
    }
}
