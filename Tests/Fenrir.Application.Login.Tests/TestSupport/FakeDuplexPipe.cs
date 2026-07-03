using System.IO.Pipelines;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     Two independent <see cref="Pipe" />s wired together as one <see cref="IDuplexPipe" /> — enough to construct a
///     <c>LoginClientSession</c> in tests without a real socket (same shape as Fenrir.Network.Tests'/
///     Fenrir.Application.Game.Tests' internal helper, duplicated here because it is deliberately not public API).
///     The test plays the role of the remote peer: read from <see cref="SessionToPeer" /> to observe what the
///     session sent.
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
