using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorStatusService(ILogger<MentorStatusService> logger) : IMentorStatusService
{
    public MentorStatusResult GetStatus(Zone zone, PlayerRuntimeState state)
    {
        if (state.TeacherCharacterId is not null && state.StudentCharacterId is not null)
            logger.LogError(
                "Mentor status invariant violated: character {CharacterId} has both TeacherCharacterId {TeacherCharacterId} and StudentCharacterId {StudentCharacterId} set -- falling back to the teacher-side check",
                state.CharacterId, state.TeacherCharacterId, state.StudentCharacterId);

        var iAmTheStudent = state.TeacherCharacterId is not null;
        var partnerId = iAmTheStudent ? state.TeacherCharacterId!.Value : state.StudentCharacterId;

        if (partnerId is null)
        {
            logger.LogWarning(
                "Mentor status rejected: character {CharacterId} has no teacher/student partner -- session will be disconnected",
                state.CharacterId);
            return new MentorStatusResult(MentorStatusResultKind.NoPartner);
        }

        if (!zone.TryGetPlayer(partnerId.Value, out var partner) || partner is null)
        {
            logger.LogDebug(
                "Mentor status ignored: character {CharacterId} partner {PartnerId} is not in map {MapId}",
                state.CharacterId, partnerId.Value, zone.MapId);
            return new MentorStatusResult(MentorStatusResultKind
                .PartnerNotInZone);
        }

        var reciprocal = iAmTheStudent
            ? partner.StudentCharacterId == state.CharacterId
            : partner.TeacherCharacterId == state.CharacterId;

        logger.LogDebug(
            "Mentor status resolved: character {CharacterId} <-> partner {PartnerId}, reciprocal {Reciprocal}",
            state.CharacterId, partnerId.Value, reciprocal);

        return new MentorStatusResult(MentorStatusResultKind.Resolved, reciprocal ? 0 : 1);
    }
}
