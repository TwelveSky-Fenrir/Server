using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_END_SEND (opcode 63) -- clears only the caller's own pointers; the partner's opposite
///     pointer is deliberately left untouched (legacy asymmetry).
/// </summary>
public sealed class MentorEndHandler(IMentorRepository repository) : IAsyncPacketHandler<MentorEndRequest>
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

        if (state.TeacherCharacterId is null && state.StudentCharacterId is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await repository.ClearForCharacterAsync(characterId, cancellationToken);

        state.TeacherCharacterId = null;
        state.StudentCharacterId = null;

        session.Send(new MentorEndResponse());
    }
}
