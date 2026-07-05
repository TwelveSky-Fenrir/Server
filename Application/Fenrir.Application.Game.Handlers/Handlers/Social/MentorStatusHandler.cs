using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TEACHER_STATE_SEND (opcode 64) -- open issue: a character with both a teacher and a student
///     prefers the teacher-side check here, not fully re-verified against source.
/// </summary>
public sealed class MentorStatusHandler(IMentorStatusService mentorStatusService)
    : IInlinePacketHandler<MentorStatusRequest>
{
    public void Handle(in MentorStatusRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = mentorStatusService.GetStatus(zone, state);

        switch (result.Kind)
        {
            case MentorStatusResultKind.NoPartner:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case MentorStatusResultKind.PartnerNotInZone:
                return;
            case MentorStatusResultKind.Resolved:
                session.Send(new MentorStatusResponse { Result = result.Result });
                return;
        }
    }
}
