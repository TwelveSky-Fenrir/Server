using System.Buffers;
using Fenrir.Application.Login.Sessions;
using Fenrir.Core.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Dispatching;

public sealed class LoginFrameDispatcher(LoginIdleClock idleClock, ILogger<LoginFrameDispatcher> logger)
    : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (LoginMessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
        {
            RearmIdleClock(opcode, session);
            return;
        }

        if (await LoginMessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
        {
            RearmIdleClock(opcode, session);
            return;
        }

        logger.LogWarning(
            "No handler registered for {Server} opcode {Opcode}, or handler present but payload failed to parse ({PayloadLength} bytes)",
            server, opcode, memory.Length);
    }

    private void RearmIdleClock(byte opcode, IPacketSession session)
    {
        if (LoginIdleClock.Rearms(opcode))
            idleClock.Rearm(session.SessionId, DateTimeOffset.UtcNow);
    }
}
