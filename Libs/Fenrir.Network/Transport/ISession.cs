using System.Net;

namespace Fenrir.Network.Transport;

public interface ISession : IAsyncDisposable
{
    
    // TODO: Should Session be separate from Buffers and Sending e.g.
    // client.session()
    // client.send()
    // The bare minimum for a client is probably to have the Socket, Disconnect?
    
    // TODO: Is TSessionData even needed anymore?
    // TODO: Why does this need to know about TMessage? Just use IMessage?
    //public TSessionData Data { get; }
    
    /// <summary>Gets the unique identifier of the underlying session.</summary>
    public string SessionId { get; }

    /// <summary>Gets the remote endpoint of the underlying session.</summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>Triggered when the session is closed.</summary>
    public CancellationToken SessionClosed { get; }

    // /// <summary>Determines whether the session is connected.</summary>
    // public bool IsConnected { get; }
    //
    // public Task ReceiveAsync();
    //
    // /// <summary>Asynchronously sends a message to the remote endpoint.</summary>
    // /// <param name="message">The message to send.</param>
    // public ValueTask SendAsync(TMessage message); // TODO: Should sending be done on Session?
    //
    // /// <summary>Disconnect the session from the remote endpoint.</summary>
    // /// <param name="delay">The delay before the session is disconnected.</param>
    // public void Disconnect(TimeSpan? delay = null);
}