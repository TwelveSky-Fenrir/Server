using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_START_SEND (opcode 62) -- master-only; consumes the accepted negotiation and bonds both
///     sides in one transaction.
/// </summary>
/// <remarks>
///     Master/student may be hosted by different zones/tick threads: only the master-side field is mutated
///     directly here; the student's side is mirrored via a zone command.
/// </remarks>
public sealed class MentorStartHandler(
    ZoneRegistry zones,
    MentorRegistry mentors,
    IMentorRepository repository,
    ILogger<MentorStartHandler> logger)
    : IAsyncPacketHandler<MentorStartRequest>
{
    public async ValueTask HandleAsync(MentorStartRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var masterId = zoneSession.CharacterId!.Value;

        if (!mentors.TryConsumeStart(masterId, out var studentId))
            return;

        if (!zones.TryGetPlayer(masterId, out var master) ||
            !zones.TryGetPlayerAndZone(studentId, out var student, out var studentZone))
            return;

        await repository.BondAsync(masterId, studentId, cancellationToken);

        master.StudentCharacterId = studentId;

        if (!studentZone.PostMentorCommand(new MentorZoneCommand(studentId, masterId)))
            logger.LogError(
                "Zone {MapId} mentor inbox full: dropped TeacherCharacterId mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                studentZone.MapId, studentId);

        master.Session.Send(new MentorStartResponse { Sort = 1, AvatarName = student.Name });
        student.Session.Send(new MentorStartResponse { Sort = 2, AvatarName = master.Name });
    }
}
