using System.Buffers;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Abstractions;

/// <summary>Routes a decoded frame; kept in Core so Network never depends on per-exe Application handler wiring.</summary>
public interface IFrameDispatcher
{
    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session,
        CancellationToken cancellationToken);
}
