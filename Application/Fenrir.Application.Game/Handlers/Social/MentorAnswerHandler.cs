using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

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
