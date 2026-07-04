using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_TEACHER_CANCEL_SEND (opcode 60) -- the master withdraws their own still-pending ask.</summary>
public sealed class MentorCancelHandler(ZoneRegistry zones, MentorRegistry mentors)
    : IInlinePacketHandler<MentorCancelRequest>
{
    public void Handle(in MentorCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var masterId = zoneSession.CharacterId!.Value;

        if (!mentors.TryCancel(masterId, out var studentId))
            return;

        if (zones.TryGetPlayer(studentId, out var student))
            student.Session.Send(new MentorCancelResponse());
    }
}
