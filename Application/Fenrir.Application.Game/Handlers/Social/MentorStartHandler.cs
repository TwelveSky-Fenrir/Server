using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_START_SEND (opcode 62) -- MASTER-only (the original asker), consumes the accepted
///     negotiation and durably bonds both sides in one transaction (<see cref="MentorRepository.BondAsync" />)
///     before mirroring onto both live <see cref="PlayerRuntimeState" />s. Sort=1 to master, Sort=2 to
///     student (verified against source).
/// </summary>
/// <remarks>
///     Master and student can be hosted by different zones/tick threads, so only this handler's own
///     master-side field is mutated directly; the student's side is mirrored via a zone command instead
///     (see inline comments below).
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

        // Self-only mutation -- same narrow, accepted posture as FriendAddHandler's own write.
        master.StudentCharacterId = studentId;

        // Cross-character: routed through the STUDENT's own hosting zone, never mutated directly here.
        if (!studentZone.PostMentorCommand(new MentorZoneCommand(studentId, masterId)))
            logger.LogError(
                "Zone {MapId} mentor inbox full: dropped TeacherCharacterId mirror for character {CharacterId} -- SQL is durable, in-memory cache will self-heal on next world entry",
                studentZone.MapId, studentId);

        master.Session.Send(new MentorStartResponse { Sort = 1, AvatarName = student.Name });
        student.Session.Send(new MentorStartResponse { Sort = 2, AvatarName = master.Name });
    }
}
