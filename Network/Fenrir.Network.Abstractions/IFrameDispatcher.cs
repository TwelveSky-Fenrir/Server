using System.Buffers;

namespace Fenrir.Network.Abstractions;

public interface IFrameDispatcher
{
    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session,
        CancellationToken cancellationToken);
}
