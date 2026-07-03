using System.IO.Pipelines;

namespace Fenrir.Network.Tests.Sessions;

/// <summary>
///     Two independent <see cref="Pipe" />s wired together as one <see cref="IDuplexPipe" /> — enough to construct a
///     <c>ClientSession</c> in tests without a real socket. The test plays the role of the remote peer: write to
///     <see cref="PeerToSession" /> to feed inbound bytes, read from <see cref="SessionToPeer" /> to observe what
///     the session sent.
/// </summary>
internal sealed class FakeDuplexPipe : IDuplexPipe
{
    private readonly Pipe _inbound = new();
    private readonly Pipe _outbound = new();

    public PipeWriter PeerToSession => _inbound.Writer;
    public PipeReader SessionToPeer => _outbound.Reader;

    public PipeReader Input => _inbound.Reader;
    public PipeWriter Output => _outbound.Writer;
}
