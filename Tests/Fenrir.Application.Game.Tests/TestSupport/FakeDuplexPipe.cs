using System.IO.Pipelines;

namespace Fenrir.Application.Game.Tests.TestSupport;

internal sealed class FakeDuplexPipe : IDuplexPipe
{
    private readonly Pipe _inbound = new();
    private readonly Pipe _outbound = new();

    public PipeWriter PeerToSession => _inbound.Writer;
    public PipeReader SessionToPeer => _outbound.Reader;

    public PipeReader Input => _inbound.Reader;
    public PipeWriter Output => _outbound.Writer;
}
