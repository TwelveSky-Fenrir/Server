using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

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
