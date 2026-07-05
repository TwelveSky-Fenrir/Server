using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_TEACHER_CANCEL_SEND (opcode 60) -- the master withdraws their own still-pending ask.</summary>
public sealed class MentorCancelHandler(ZoneRegistry zones, IMentorCancelService mentorCancelService)
    : IInlinePacketHandler<MentorCancelRequest>
{
    public void Handle(in MentorCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var masterId = zoneSession.CharacterId!.Value;

        var result = mentorCancelService.Cancel(masterId);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.StudentId, out var student))
            student.Session.Send(new MentorCancelResponse());
    }
}
