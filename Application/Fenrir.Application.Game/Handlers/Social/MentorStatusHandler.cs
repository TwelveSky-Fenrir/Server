using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_STATE_SEND (opcode 64) -- neither a teacher nor a student ⇒ Quit(). Checks reciprocity
///     with the ONLINE partner in this SAME zone; an offline/other-zone partner gets no reply at all
///     (contract's own "partenaire hors zone = pas de réponse"). Reasonable, documented reading where the
///     source's own dual-partner shape (a character with BOTH a teacher AND a student, not fully
///     re-verified against a single-partner-checking source citation) is ambiguous -- prefers the
///     teacher-side reciprocity check when both exist (open issue).
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
