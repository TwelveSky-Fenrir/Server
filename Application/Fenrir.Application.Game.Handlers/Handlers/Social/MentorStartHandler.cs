using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TEACHER_START_SEND (opcode 62) -- master-only; consumes the accepted negotiation and bonds both
///     sides in one transaction.
/// </summary>
/// <remarks>
///     <para>
///         Master/student may be hosted by different zones/tick threads: only the master-side field is mutated
///         directly here; the student's side is mirrored via a zone command.
///     </para>
///     <para>
///         <b>LEGACY-PARITY RISK (open, not resolved by inference):</b> the "master-only" restriction below
///         (delegated to <see cref="Fenrir.Application.Game.Domain.Social.Mentor.MentorRegistry.TryConsumeStart" />)
///         is an uncorroborated assumption -- see that method's own remarks for the full citation and
///         correction. Direct re-reading of Server/ts25zone/S04_MyWork02.cpp:9406-9499 shows the server itself
///         does not gate MentorStart to the original asker; do not change this handler to permit a
///         student-side start without a fresh legacy-research finding confirming that reading is correct.
///     </para>
/// </remarks>
public sealed class MentorStartHandler(
    ZoneRegistry zones,
    IMentorStartService mentorStartService,
    ILogger<MentorStartHandler> logger) : IAsyncPacketHandler<MentorStartRequest>
{
    public async ValueTask HandleAsync(MentorStartRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("MentorStart: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var masterId = zoneSession.CharacterId!.Value;

        if (!mentorStartService.TryConsumeStart(masterId, out var studentId))
            return;

        if (!zones.TryGetPlayer(masterId, out var master) ||
            !zones.TryGetPlayerAndZone(studentId, out var student, out var studentZone))
            return;

        await mentorStartService.BondAsync(master, student, studentZone, cancellationToken);

        master.Session.Send(new MentorStartResponse { Sort = 1, AvatarName = student.Name });
        student.Session.Send(new MentorStartResponse { Sort = 2, AvatarName = master.Name });
    }
}
