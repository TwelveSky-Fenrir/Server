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
///     before mirroring onto both live <see cref="PlayerRuntimeState" />s. <c>Sort=1</c> to the master
///     (attached name = new student), <c>Sort=2</c> to the student (attached name = new master) --
///     verified, the inverse of an earlier inventory pass's note.
/// </summary>
/// <remarks>
///     Review finding (Phase C/V6): this used to set <c>student.TeacherCharacterId = masterId;</c> DIRECTLY
///     from the master's own request thread -- a genuine single-writer-invariant violation (the master and
///     student can be hosted by two different zones/tick threads), unlike this handler's OWN
///     <see cref="PlayerRuntimeState.StudentCharacterId" /> write, which stays a direct self-mutation (the
///     same narrow, accepted posture <c>FriendAddHandler</c> uses). The student's own field is now mirrored
///     via <see cref="MentorZoneCommand" />, posted to the student's OWN hosting zone (resolved via
///     <see cref="ZoneRegistry.TryGetPlayerAndZone" />), exactly like <c>GenericActionHandler</c> already
///     does for Inventory/Skill mirrors.
/// </remarks>
public sealed class MentorStartHandler(
    ZoneRegistry zones,
    MentorRegistry mentors,
    MentorRepository repository,
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
