using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_END_SEND (opcode 63) -- clears only the caller's own pointers; the partner's opposite
///     pointer is deliberately left untouched (legacy asymmetry).
/// </summary>
public sealed class MentorEndHandler(IMentorEndService mentorEndService) : IAsyncPacketHandler<MentorEndRequest>
{
    public async ValueTask HandleAsync(MentorEndRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

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
