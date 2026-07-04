using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_STATE_SEND (opcode 64) -- neither teacher nor student ⇒ Quit(). Checks reciprocity with
///     the online partner in the same zone only; an offline/other-zone partner gets no reply (contract's
///     own "partenaire hors zone = pas de réponse"). Open issue: a character with both a teacher and a
///     student prefers the teacher-side check here, not fully re-verified against source.
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
            return; // partner not in this same zone -- no reply at all (verified)

        // Reciprocal: if I consider PARTNER my teacher, they must consider ME their student, and vice versa.
        var reciprocal = iAmTheStudent
            ? partner.StudentCharacterId == characterId
            : partner.TeacherCharacterId == characterId;

        session.Send(new MentorStatusResponse { Result = reciprocal ? 0 : 1 });
    }
}
