using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TEACHER_ANSWER_SEND (opcode 61) -- on accept, the master (not the student) later consumes it via
///     CZ_TEACHER_START_SEND.
/// </summary>
public sealed class MentorAnswerHandler(ZoneRegistry zones, IMentorAnswerService mentorAnswerService)
    : IInlinePacketHandler<MentorAnswerRequest>
{
    public void Handle(in MentorAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var studentId = zoneSession.CharacterId!.Value;

        var result = mentorAnswerService.Answer(studentId, packet.Answer);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.MasterId, out var master))
            master.Session.Send(new MentorAnswerResponse { Answer = packet.Answer });
    }
}
