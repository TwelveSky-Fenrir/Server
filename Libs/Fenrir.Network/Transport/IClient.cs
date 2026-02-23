namespace Fenrir.Network.Transport;

public interface IClient : IAsyncDisposable
{
    /// <summary>Gets the receive buffer for the session.</summary>
    byte[] RecvBuffer { get; }
    // TODO: Span?
    
    // TODO: Send buffer thread safe queue?

    uint RecvBufferPosition { get; set; }
    ISession? Session { get; set; }
    Task ReceiveAsync();
}