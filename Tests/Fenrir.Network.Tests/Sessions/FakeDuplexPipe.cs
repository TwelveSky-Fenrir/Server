using System.IO.Pipelines;

namespace Fenrir.Network.Tests.Sessions;

// The test plays the remote peer: write to PeerToSession to feed inbound bytes, read from SessionToPeer to
// observe what the session sent.
internal sealed class FakeDuplexPipe : IDuplexPipe
{
    private readonly Pipe _inbound = new();
    private readonly Pipe _outbound = new();

    public PipeWriter PeerToSession => _inbound.Writer;
    public PipeReader SessionToPeer => _outbound.Reader;

    public PipeReader Input => _inbound.Reader;
    public PipeWriter Output => _outbound.Writer;
}
