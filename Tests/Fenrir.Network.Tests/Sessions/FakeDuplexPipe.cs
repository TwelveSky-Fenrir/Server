using System.IO.Pipelines;

namespace Fenrir.Network.Tests.Sessions;

internal sealed class FakeDuplexPipe : IDuplexPipe
{
    private readonly Pipe _inbound = new();
    private readonly Pipe _outbound;

    public FakeDuplexPipe(PipeOptions? outboundOptions = null)
    {
        _outbound = new Pipe(outboundOptions ?? PipeOptions.Default);
    }

    public PipeWriter PeerToSession => _inbound.Writer;
    public PipeReader SessionToPeer => _outbound.Reader;

    public PipeReader Input => _inbound.Reader;
    public PipeWriter Output => _outbound.Writer;
}
