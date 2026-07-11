using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Tests.TestSupport;

internal sealed class ThrowingFrameDispatcher(Exception exception) : IFrameDispatcher
{
    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        throw exception;
    }
}
