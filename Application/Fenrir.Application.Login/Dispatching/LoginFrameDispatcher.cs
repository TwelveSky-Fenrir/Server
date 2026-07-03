using System.Buffers;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Dispatch;
using Fenrir.Contracts.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Dispatching;

/// <summary>
///     Bridges <see cref="Fenrir.Network.Dispatching.SessionLoop" /> to the <c>MessageDispatcher</c> generated
///     from every <c>IInlinePacketHandler{T}</c>/<c>IAsyncPacketHandler{T}</c> declared in this assembly
///     (<see cref="Fenrir.Generators.Dispatch.HandlerDispatchIncrementalGenerator" />, wired as an analyzer on
///     this project — see Fenrir.Application.Login.csproj). Tries the inline (sync) table first, then the
///     async one, matching the two independent switch tables the generator emits.
/// </summary>
public sealed class LoginFrameDispatcher(ILogger<LoginFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        // Login payloads are small (largest incoming is CL_LOGIN_SEND at 457 B) and normally arrive as a single
        // pipe segment; ToArray() only triggers on the rare fragmented case instead of on every dispatch.
        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (MessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return;

        if (await MessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return;

        // Unreachable for the real protocol: FrameDecoder/OpcodeRegistry already rejected any opcode the wire
        // contract doesn't declare, and SessionStateGate already rejected one illegal in the current state —
        // reaching here means an M1 opcode has no registered handler, which is an implementation gap, not a
        // hostile/malformed packet. Logged rather than disconnected: the client did nothing wrong.
        logger.LogWarning("No handler registered for {Server} opcode {Opcode}", server, opcode);
    }
}
