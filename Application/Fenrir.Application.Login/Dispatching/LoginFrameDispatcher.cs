using System.Buffers;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Dispatch;
using Fenrir.Contracts.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Dispatching;

/// <summary>
///     Bridges <see cref="Fenrir.Network.Dispatching.SessionLoop" /> to the generated <c>MessageDispatcher</c>
///     (inline table, then async).
/// </summary>
public sealed class LoginFrameDispatcher(ILogger<LoginFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        // Avoids ToArray() on the common single-segment case; login payloads are small and rarely fragmented.
        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (MessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return;

        if (await MessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return;

        // Reaching here means a registered opcode has no handler (implementation gap); FrameDecoder/SessionStateGate
        // already rejected unknown/illegal opcodes, so log instead of disconnecting — the client did nothing wrong.
        logger.LogWarning("No handler registered for {Server} opcode {Opcode}", server, opcode);
    }
}
