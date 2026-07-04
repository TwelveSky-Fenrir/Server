using System.Buffers;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Dispatch;
using Fenrir.Contracts.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Dispatching;

/// <summary>Bridges <see cref="Fenrir.Network.Dispatching.SessionLoop" /> to the generated <c>MessageDispatcher</c> for this assembly's packet handlers.</summary>
public sealed class ZoneFrameDispatcher(ILogger<ZoneFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (MessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return;

        if (await MessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return;

        logger.LogWarning("No handler registered for {Server} opcode {Opcode}", server, opcode);
    }
}
