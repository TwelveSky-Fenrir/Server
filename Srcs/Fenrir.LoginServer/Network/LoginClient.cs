using Fenrir.Network.Transport;

namespace Fenrir.LoginServer.Network;

public class LoginClient : ClientBase
{
    public async Task ReceiveAsync()
    {
        throw new NotImplementedException();
        // try
        // {
        //     while (!_cts.IsCancellationRequested)
        //     {
        //         var readResult = await _pipe.Input.ReadAsync(_cts.Token).ConfigureAwait(false);
        //
        //         if (readResult.IsCanceled)
        //             break;
        //
        //         var buffer = readResult.Buffer;
        //
        //         // try
        //         // {
        //         //     foreach (var message in _messageDecoder.DecodeMessages(buffer, true))
        //         //     {
        //         //         var dispatchResult =
        //         //             await _messageDispatcher.DispatchAsync(this, message).ConfigureAwait(false);
        //         //
        //         //         if (!_options.EnableLogging || !_logger.IsEnabled(LogLevel.Debug))
        //         //             continue;
        //         //
        //         //         // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        //         //         _logger.LogDebug(dispatchResult switch
        //         //         {
        //         //             DispatchResults.Succeeded => "Session ({Name}) dispatched message ({Message}) successfully",
        //         //             DispatchResults.Failed => "Session ({Name}) failed to dispatch message ({Message})",
        //         //             DispatchResults.NotMapped =>
        //         //                 "Session ({Name}) failed to dispatch message ({Message}) because it is not mapped",
        //         //             _ => throw new ArgumentOutOfRangeException(nameof(dispatchResult), dispatchResult, null)
        //         //         }, ToString(), message);
        //         //     }
        //         //
        //         //     if (readResult.IsCompleted)
        //         //     {
        //         //         if (!buffer.IsEmpty)
        //         //             throw new InvalidOperationException("Incomplete message received");
        //         //
        //         //         break;
        //         //     }
        //         // }
        //         // finally
        //         // {
        //         //     _pipe.Input.AdvanceTo(buffer.Start, buffer.End);
        //         // }
        //     }
        // }
        // catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException)
        // {
        //     /* ignore */
        // }
    }
    
}