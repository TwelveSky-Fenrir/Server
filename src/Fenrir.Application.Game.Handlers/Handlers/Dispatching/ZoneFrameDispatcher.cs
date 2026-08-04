using System.Buffers;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Wire;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Dispatching;

public sealed class ZoneFrameDispatcher(ILogger<ZoneFrameDispatcher> logger) : IFrameDispatcher
{
    public async ValueTask<FrameDispatchOutcome> DispatchAsync(FenrirServer server, byte opcode,
        ReadOnlySequence<byte> payload,
        IPacketSession session, CancellationToken cancellationToken)
    {
        if (IsWithheldByPendingZoneTransfer(session, opcode))
            return FrameDispatchOutcome.Withheld;

        var memory = payload.IsSingleSegment ? payload.First : payload.ToArray();

        if (ZoneMessageDispatcher.TryHandleInline(server, opcode, memory.Span, session))
            return FrameDispatchOutcome.Handled;

        if (await ZoneMessageDispatcher.TryHandleAsync(server, opcode, memory, session, cancellationToken)
                .ConfigureAwait(false))
            return FrameDispatchOutcome.Handled;

        logger.LogWarning(
            "Zone session {SessionId}: closing after unhandled framed {Server} opcode {Opcode}; no handler accepted its {PayloadLength}-byte payload",
            session.SessionId, server, opcode, memory.Length);

        session.Abort(DisconnectReason.UnknownOpcode);
        return FrameDispatchOutcome.Terminated;
    }

    private static bool IsWithheldByPendingZoneTransfer(IPacketSession session, byte opcode)
    {
        if (session is not IZoneSession zoneSession)
            return false;

        var actorTransferPending = zoneSession is { CurrentZone: Zone zone, CharacterId: { } characterId } &&
                                   zone.TryGetPlayer(characterId, out var state) && state is not null &&
                                   state.IsMovingZone;

        return ZoneTransferFreezeGate.ShouldWithhold(zoneSession.IsZoneTransferPending || actorTransferPending,
            opcode, Opcodes.Zone.Incoming.ZoneTransferCancel, Opcodes.Zone.Incoming.ZoneHandshake,
            Opcodes.Zone.Incoming.EnterWorld);
    }
}
