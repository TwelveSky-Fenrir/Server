using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Dispatching;

/// <summary>
///     Bridges <see cref="SessionLoop" /> to the generated <c>MessageDispatcher</c>
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

        // Reaching here is ambiguous between two different situations that TryHandleInline/TryHandleAsync's own
        // bool-only return can't distinguish: (a) the opcode genuinely has no registered handler
        // (implementation gap), or (b) a handler IS registered but its generated TryRead rejected the payload
        // (malformed/truncated frame -- possibly a wire-desync or a hostile client, a materially more
        // concerning event than (a)). FrameDecoder/SessionStateGate already rejected unknown/illegal opcodes
        // before this ever runs, so either way this logs instead of disconnecting -- the client did nothing
        // FrameDecoder itself considers wrong. memory.Length gives an operator a size signal to eyeball for
        // truncation even without being able to tell (a) from (b) here.
        logger.LogWarning(
            "No handler registered for {Server} opcode {Opcode}, or handler present but payload failed to parse ({PayloadLength} bytes)",
            server, opcode, memory.Length);
    }
}
