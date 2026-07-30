using System.Buffers;
using Fenrir.Core.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Wire;

public sealed class CenterFrameDispatcher(ILogger<CenterFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (CenterMessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return;

        if (await CenterMessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return;

        logger.LogWarning(
            "No CenterServer handler registered for {Server} opcode {Opcode}, or handler present but payload failed to parse ({PayloadLength} bytes)",
            server, opcode, memory.Length);
    }
}
