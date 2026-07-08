using System.IO;
using System.Net.Sockets;
using Fenrir.Network.Transport;

namespace Fenrir.Network.Tests.Transport;

public sealed class TransportFaultClassifierTests
{
    [Fact]
    public void SocketException_IsExpectedDisconnect()
    {
        var ex = new SocketException((int)SocketError.ConnectionReset);

        Assert.True(TransportFaultClassifier.IsExpectedDisconnect(ex));
    }

    [Fact]
    public void ObjectDisposedException_IsExpectedDisconnect()
    {
        var ex = new ObjectDisposedException(nameof(Socket));

        Assert.True(TransportFaultClassifier.IsExpectedDisconnect(ex));
    }

    [Fact]
    public void IOExceptionWrappingSocketException_IsExpectedDisconnect()
    {
        var ex = new IOException("wrapped", new SocketException((int)SocketError.ConnectionAborted));

        Assert.True(TransportFaultClassifier.IsExpectedDisconnect(ex));
    }

    [Fact]
    public void IOExceptionWithoutSocketInnerException_IsNotExpectedDisconnect()
    {
        var ex = new IOException("disk full");

        Assert.False(TransportFaultClassifier.IsExpectedDisconnect(ex));
    }

    [Fact]
    public void NullReferenceException_IsNotExpectedDisconnect()
    {
        var ex = new NullReferenceException();

        Assert.False(TransportFaultClassifier.IsExpectedDisconnect(ex));
    }
}
