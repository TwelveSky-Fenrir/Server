using System.Buffers;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Dispatching;

public sealed class ZoneFrameDispatcher(ILogger<ZoneFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask DispatchAsync(FenrirServer server, byte opcode, ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        if (IsWithheldByPendingZoneTransfer(session, opcode))
            return;

        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (ZoneMessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return;

        if (await ZoneMessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return;

        logger.LogWarning(
            "No handler registered for {Server} opcode {Opcode}, or handler present but payload failed to parse ({PayloadLength} bytes)",
            server, opcode, memory.Length);
    }

    private static bool IsWithheldByPendingZoneTransfer(IPacketSession session, byte opcode)
    {
        return session is IZoneSession { CurrentZone: Domain.World.Zone zone, CharacterId: { } characterId } &&
               zone.TryGetPlayer(characterId, out var state) && state is not null &&
               ZoneTransferFreezeGate.ShouldWithhold(state.IsMovingZone, opcode,
                   Opcodes.Zone.Incoming.ZoneTransferCancel);
    }
}
