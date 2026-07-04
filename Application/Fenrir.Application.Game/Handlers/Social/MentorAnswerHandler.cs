using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_ANSWER_SEND (opcode 61). 0 = accept, 1/2 = refuse, else ignored. On accept, the MASTER (not the
///     student) later consumes the acceptance via CZ_TEACHER_START_SEND.
/// </summary>
public sealed class MentorAnswerHandler(ZoneRegistry zones, MentorRegistry mentors)
    : IInlinePacketHandler<MentorAnswerRequest>
{
    public void Handle(in MentorAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var studentId = zoneSession.CharacterId!.Value;

        if (!mentors.TryAnswer(studentId, packet.Answer == 0, out var masterId))
            return;

        if (zones.TryGetPlayer(masterId, out var master))
            master.Session.Send(new MentorAnswerResponse { Answer = packet.Answer });
    }
}
