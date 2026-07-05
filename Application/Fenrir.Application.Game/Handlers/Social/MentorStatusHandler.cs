using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_STATE_SEND (opcode 64) -- open issue: a character with both a teacher and a student
///     prefers the teacher-side check here, not fully re-verified against source.
/// </summary>
public sealed class MentorStatusHandler : IInlinePacketHandler<MentorStatusRequest>
{
    public void Handle(in MentorStatusRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var iAmTheStudent = state.TeacherCharacterId is not null;
        var partnerId = iAmTheStudent ? state.TeacherCharacterId!.Value : state.StudentCharacterId;

        if (partnerId is null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!zone.TryGetPlayer(partnerId.Value, out var partner) || partner is null)
            return; // partner not in this same zone -- no reply

        var reciprocal = iAmTheStudent
            ? partner.StudentCharacterId == characterId
            : partner.TeacherCharacterId == characterId;

        session.Send(new MentorStatusResponse { Result = reciprocal ? 0 : 1 });
    }
}
