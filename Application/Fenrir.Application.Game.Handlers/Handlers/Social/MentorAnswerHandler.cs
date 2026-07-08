using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TEACHER_ANSWER_SEND (opcode 61) -- on accept, the master (not the student) later consumes it via
///     CZ_TEACHER_START_SEND. See
///     <see cref="Fenrir.Application.Game.Domain.Social.Mentor.MentorRegistry.TryConsumeStart" />
///     for an open LEGACY-PARITY RISK note: whether that master-only restriction on MentorStart is
///     legacy-accurate is unconfirmed.
/// </summary>
public sealed class MentorAnswerHandler(
    ZoneRegistry zones,
    IMentorAnswerService mentorAnswerService,
    ILogger<MentorAnswerHandler> logger) : IInlinePacketHandler<MentorAnswerRequest>
{
    public void Handle(in MentorAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("MentorAnswer: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, zoneSession.CharacterId, packet.Answer);

        var studentId = zoneSession.CharacterId!.Value;

        var result = mentorAnswerService.Answer(studentId, packet.Answer);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.MasterId, out var master))
            master.Session.Send(new MentorAnswerResponse { Answer = packet.Answer });
    }
}
