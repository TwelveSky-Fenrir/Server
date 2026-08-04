using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Transport.Logging;
using Microsoft.Extensions.Logging;

namespace Fenrir.Network.Transport;

public sealed class SocketConnection : IBufferedDuplexPipe, IAsyncDisposable
{
    private const int ReceiveBufferSize = 4096;

    private const int SocketSendBufferSize = 204800;
    private const int SocketReceiveBufferSize = 20480;

    private readonly CancellationTokenSource _abortCts = new();
    private readonly ILogger? _logger;
    private readonly Pipe _rxPipe;

    private readonly Socket _socket;
    private readonly Pipe _txPipe;
    private readonly PipeWriter _txWriter;

    private long _bufferedOutputBytes;
    private int _disposed;
    private int _faultReported;

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
        _txWriter = new CountingPipeWriter(_txPipe.Writer, OnOutputBytesWritten);
    }

    public IPEndPoint? RemoteEndPoint { get; }

    public Func<byte> GetInboundXorKey { get; set; } = static () => 0;

    public long BufferedOutputBytes => Volatile.Read(ref _bufferedOutputBytes);

    public event Action<long>? OutputBytesConsumed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Abort();
        _socket.Dispose();

        await CompleteQuietlyAsync(_rxPipe.Reader).ConfigureAwait(false);
        await CompleteQuietlyAsync(_rxPipe.Writer).ConfigureAwait(false);
        await CompleteQuietlyAsync(_txPipe.Reader).ConfigureAwait(false);
        await CompleteQuietlyAsync(_txPipe.Writer).ConfigureAwait(false);

        _abortCts.Dispose();
    }

    public PipeReader Input => _rxPipe.Reader;
    public PipeWriter Output => _txWriter;

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

            if (!cancellationToken.IsCancellationRequested)
                ReportFault(ex);
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

                        if (sent <= 0)
                            throw new IOException(
                                $"Socket.SendAsync reported {sent} byte(s) sent for a {remaining.Length}-byte " +
                                "segment; the send loop cannot make progress and would spin.");

                        remaining = remaining[sent..];
                    }
                }

                reader.AdvanceTo(buffer.End);
                ReportOutputBytesConsumed(buffer.Length);

                if (result.IsCompleted || result.IsCanceled)
                    break;
            }
        }
        catch (Exception ex)
        {
            failure = ex;

            if (ex is not OperationCanceledException)
            {
                _logger?.SendLoopFaulted(ex, RemoteEndPoint);
                ReportFault(ex);
            }
        }
        finally
        {
            await reader.CompleteAsync(failure).ConfigureAwait(false);
        }
    }

    private static async ValueTask CompleteQuietlyAsync(PipeReader reader)
    {
        try
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static async ValueTask CompleteQuietlyAsync(PipeWriter writer)
    {
        try
        {
            await writer.CompleteAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnOutputBytesWritten(int bytes)
    {
        if (bytes > 0)
            Interlocked.Add(ref _bufferedOutputBytes, bytes);
    }

    private void ReportOutputBytesConsumed(long bytes)
    {
        if (bytes <= 0)
            return;

        Interlocked.Add(ref _bufferedOutputBytes, -bytes);
        try
        {
            OutputBytesConsumed?.Invoke(bytes);
        }
        catch
        {
        }
    }

    private void ReportFault(Exception exception)
    {
        if (Interlocked.CompareExchange(ref _faultReported, 1, 0) != 0)
            return;

        try
        {
            TransportFaulted?.Invoke(exception);
        }
        catch
        {
        }
    }

    public event Action<Exception>? TransportFaulted;

    private sealed class CountingPipeWriter(PipeWriter inner, Action<int> onAdvance) : PipeWriter
    {
        public override bool CanGetUnflushedBytes => inner.CanGetUnflushedBytes;

        public override long UnflushedBytes => inner.UnflushedBytes;

        public override void Advance(int bytes)
        {
            inner.Advance(bytes);
            onAdvance(bytes);
        }

        public override Stream AsStream(bool leaveOpen = false)
        {
            return inner.AsStream(leaveOpen);
        }

        public override void CancelPendingFlush()
        {
            inner.CancelPendingFlush();
        }

        public override void Complete(Exception? exception = null)
        {
            inner.Complete(exception);
        }

        public override ValueTask CompleteAsync(Exception? exception = null)
        {
            return inner.CompleteAsync(exception);
        }

        public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
        {
            return inner.FlushAsync(cancellationToken);
        }

        public override Memory<byte> GetMemory(int sizeHint = 0)
        {
            return inner.GetMemory(sizeHint);
        }

        public override Span<byte> GetSpan(int sizeHint = 0)
        {
            return inner.GetSpan(sizeHint);
        }

        [Obsolete]
        public override void OnReaderCompleted(Action<Exception?, object?> callback, object? state)
        {
            inner.OnReaderCompleted(callback, state);
        }

        public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source,
            CancellationToken cancellationToken = default)
        {
            source.CopyTo(GetMemory(source.Length));
            Advance(source.Length);
            return FlushAsync(cancellationToken);
        }
    }
}
