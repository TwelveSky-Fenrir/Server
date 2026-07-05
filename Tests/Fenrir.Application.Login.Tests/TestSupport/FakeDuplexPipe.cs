using System.IO.Pipelines;

namespace Fenrir.Application.Login.Tests.TestSupport;

// Duplicated from Fenrir.Network.Tests' helper of the same shape since it's deliberately not public API.
// The test plays the remote peer: read from SessionToPeer to observe what the session sent.
internal sealed class FakeDuplexPipe : IDuplexPipe
{
    private readonly Pipe _inbound = new();
    private readonly Pipe _outbound = new();

    public PipeWriter PeerToSession => _inbound.Writer;
    public PipeReader SessionToPeer => _outbound.Reader;

    public PipeReader Input => _inbound.Reader;
    public PipeWriter Output => _outbound.Writer;
}
