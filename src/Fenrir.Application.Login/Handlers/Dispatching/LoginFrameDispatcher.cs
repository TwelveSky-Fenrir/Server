using System.Buffers;
using Fenrir.Core.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Dispatching;

public sealed class LoginFrameDispatcher(ILogger<LoginFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (LoginMessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return;

        if (await LoginMessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return;

        logger.LogWarning(
            "No handler registered for {Server} opcode {Opcode}, or handler present but payload failed to parse ({PayloadLength} bytes)",
            server, opcode, memory.Length);
    }
}
