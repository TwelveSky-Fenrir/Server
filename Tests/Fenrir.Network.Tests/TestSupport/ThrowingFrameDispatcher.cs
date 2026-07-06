using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Tests.TestSupport;

// Exercises SessionLoop's dispatch exception boundary: DispatchAsync throws synchronously (before
// returning a ValueTask), so the exception surfaces at the caller's await exactly like a throwing
// generated MessageDispatcher call would.
internal sealed class ThrowingFrameDispatcher(Exception exception) : IFrameDispatcher
{
    public ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        throw exception;
    }
}
