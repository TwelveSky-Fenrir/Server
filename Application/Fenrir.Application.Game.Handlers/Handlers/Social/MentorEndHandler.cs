using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TEACHER_END_SEND (opcode 63) -- clears only the caller's own pointers; the partner's opposite
///     pointer is deliberately left untouched (legacy asymmetry).
/// </summary>
public sealed class MentorEndHandler(IMentorEndService mentorEndService, ILogger<MentorEndHandler> logger)
    : IAsyncPacketHandler<MentorEndRequest>
{
    public async ValueTask HandleAsync(MentorEndRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("MentorEnd: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await mentorEndService.EndAsync(state, cancellationToken);

        if (result.Kind == MentorEndResultKind.NotBonded)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new MentorEndResponse());
    }
}
