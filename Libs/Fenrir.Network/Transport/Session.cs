using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Fenrir.Network.Dispatcher;
using Fenrir.Network.Framing;
using Fenrir.Network.Infrastructure;
using Fenrir.Network.Options;
using Microsoft.Extensions.Logging;

#pragma warning disable MA0015 // 'dispatchResult' is not a valid parameter name

namespace Fenrir.Network.Transport;
/// <summary>A network session that represents a connection to a remote endpoint.</summary>
public abstract class Session : ISession
{
    private readonly CancellationTokenSource _cts;
    private readonly ILogger _logger;
    // private readonly IMessageDecoder<TMessage> _messageDecoder;
    // private readonly IMessageDispatcher<TPacketType, TSessionData, TMessage> _messageDispatcher;
    // protected readonly IMessageEncoder<TMessage> _messageEncoder;
    private readonly FenrirServerOptions _options;

    // protected readonly IDuplexPipe _pipe;
    // private readonly Socket _socket;

    public IPEndPoint RemoteEndPoint { get; }
    
    private bool _disposed;
    private string? _sessionId; // TODO: Why a session id?

    private string? _accountId; // TODO: How does it know what account?
    private bool _authenticated = false;

    protected Session(
        // Socket socket,
        // IMessageParser<TMessage> messageParser,
        // IMessageDispatcher<TPacketType, TSessionData, TMessage> messageDispatcher,
        ILogger logger,
        FenrirServerOptions options, string? sessionId, IPEndPoint remoteEndPoint)
    {
        // _socket = socket;
        // _messageDecoder = messageParser;
        // _messageEncoder = messageParser;
        // _messageDispatcher = messageDispatcher;
        _cts = new CancellationTokenSource();
        // _pipe = DuplexPipe.Create(socket);
        _logger = logger;
        _options = options;
        _sessionId = sessionId;
        RemoteEndPoint = remoteEndPoint;
    }
    
    // public TSessionData Data { get; }

    /// <summary>Gets the unique identifier of the underlying session.</summary>
    public string SessionId => _sessionId ??= UuidGenerator.NewGuid();
    //
    // /// <summary>Gets the remote endpoint of the underlying session.</summary>
    // public IPEndPoint RemoteEndPoint => (IPEndPoint)_socket.RemoteEndPoint!;

    /// <summary>Triggered when the session is closed.</summary>
    public CancellationToken SessionClosed => _cts.Token;

    // /// <summary>Determines whether the session is connected.</summary>
    // public bool IsConnected => !_disposed && _socket.Connected && !_cts.IsCancellationRequested;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        //Disconnect();

        // await _pipe.Input.CompleteAsync().ConfigureAwait(false);
        // await _pipe.Output.CompleteAsync().ConfigureAwait(false);

        // try
        // {
        //     _socket.Shutdown(SocketShutdown.Both);
        // }
        // catch (SocketException)
        // {
        //     /* ignore */
        // }
        //
        // _socket.Close();
        // _socket.Dispose();
        _cts.Dispose();

        GC.SuppressFinalize(this);
    }

    // public async Task ReceiveAsync()
    // {
    //     try
    //     {
    //         while (!_cts.IsCancellationRequested)
    //         {
    //             var readResult = await _pipe.Input.ReadAsync(_cts.Token).ConfigureAwait(false);
    //
    //             if (readResult.IsCanceled)
    //                 break;
    //
    //             var buffer = readResult.Buffer;
    //
    //             try
    //             {
    //                 foreach (var message in _messageDecoder.DecodeMessages(buffer, true))
    //                 {
    //                     var dispatchResult =
    //                         await _messageDispatcher.DispatchAsync(this, message).ConfigureAwait(false);
    //
    //                     if (!_options.EnableLogging || !_logger.IsEnabled(LogLevel.Debug))
    //                         continue;
    //
    //                     // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
    //                     _logger.LogDebug(dispatchResult switch
    //                     {
    //                         DispatchResults.Succeeded => "Session ({Name}) dispatched message ({Message}) successfully",
    //                         DispatchResults.Failed => "Session ({Name}) failed to dispatch message ({Message})",
    //                         DispatchResults.NotMapped =>
    //                             "Session ({Name}) failed to dispatch message ({Message}) because it is not mapped",
    //                         _ => throw new ArgumentOutOfRangeException(nameof(dispatchResult), dispatchResult, null)
    //                     }, ToString(), message);
    //                 }
    //
    //                 if (readResult.IsCompleted)
    //                 {
    //                     if (!buffer.IsEmpty)
    //                         throw new InvalidOperationException("Incomplete message received");
    //
    //                     break;
    //                 }
    //             }
    //             finally
    //             {
    //                 _pipe.Input.AdvanceTo(buffer.Start, buffer.End);
    //             }
    //         }
    //     }
    //     catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
    //     {
    //         /* ignore */
    //     }
    // }
    //
    // /// <summary>Asynchronously sends a message to the remote endpoint.</summary>
    // /// <param name="message">The message to send.</param>
    // public ValueTask SendAsync(TMessage message)
    // {
    //     if (_disposed)
    //         throw new ObjectDisposedException(nameof(Session<TPacketType, TSessionData, TMessage>));
    //
    //     if (_cts.IsCancellationRequested)
    //         return ValueTask.CompletedTask;
    //
    //     var buffer = _messageEncoder.EncodeMessage(message, true);
    //
    //     var flushTask = buffer.IsEmpty
    //         ? _pipe.Output.FlushAsync(_cts.Token)
    //         : _pipe.Output.WriteAsync(buffer, _cts.Token);
    //
    //     return !flushTask.IsCompletedSuccessfully
    //         ? FireAndForget(flushTask)
    //         : ValueTask.CompletedTask;
    //
    //     static async ValueTask FireAndForget(ValueTask<FlushResult> flushTask)
    //     {
    //         await flushTask.ConfigureAwait(false);
    //     }
    // }
    //
    // /// <summary>Disconnect the session from the remote endpoint.</summary>
    // /// <param name="delay">The delay before the session is disconnected.</param>
    // public void Disconnect(TimeSpan? delay = null)
    // {
    //     if (_cts.IsCancellationRequested)
    //         return;
    //
    //     if (delay.HasValue)
    //         _cts.CancelAfter(delay.Value);
    //     else
    //         _cts.Cancel();
    //
    //     _pipe.Input.CancelPendingRead();
    //     _pipe.Output.CancelPendingFlush();
    // }

    /// <inheritdoc />
    // public override string ToString()
    // {
    //     return $"({RemoteEndPoint})";
    // }
}