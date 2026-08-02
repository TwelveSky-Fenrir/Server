using System.Buffers;
using Fenrir.Core.Wire;

namespace Fenrir.Core.Abstractions;

public interface IFrameDispatcher
{
    public ValueTask<FrameDispatchOutcome> DispatchAsync(FenrirServer server, byte opcode,
        ReadOnlySequence<byte> payload,
        IPacketSession session,
        CancellationToken cancellationToken);
}
