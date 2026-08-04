using System.IO.Pipelines;

namespace Fenrir.Network.Transport;

public interface IBufferedDuplexPipe : IDuplexPipe
{
    public long BufferedOutputBytes { get; }

    public event Action<long>? OutputBytesConsumed;
}
