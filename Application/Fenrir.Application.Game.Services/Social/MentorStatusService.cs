using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <remarks>
///     Which side "wins" when both <see cref="PlayerRuntimeState.TeacherCharacterId" /> and
///     <see cref="PlayerRuntimeState.StudentCharacterId" /> are set is moot, not an unresolved guess: the
///     already-cited Mentor-Ask (op59) failure semantics disconnect the asker if it already holds either
///     field, and soft-fail (reply 6/7) any ask that targets a character already holding either field
///     (Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:9303-9372). That gate means a character can hold at
///     most one of the two fields at a time under correct legacy play -- the ambiguous "both set" case
///     this class used to flag as unverified cannot legitimately arise. <see cref="MentorAskService" /> /
///     <see cref="MentorRegistry" /> enforce the identical asker-side and target-side gate on the Fenrir
///     side, and every other write path that touches these fields (MentorStart's bond, MentorEnd's clear,
///     tribe migration's block-if-bonded check, and world-entry load from the durable row) upholds it too
///     -- see MentorAskService.Ask, MentorRegistry.TryAsk, MentorStartService.BondAsync, MentorEndService.EndAsync,
///     TribeMigrationGate. <see cref="GetStatus" /> still defends against the theoretically-unreachable
///     both-set state below (logs it as an invariant violation rather than silently guessing) in case a
///     future bug ever breaks that invariant.
/// </remarks>
public sealed class MentorStatusService(ILogger<MentorStatusService> logger) : IMentorStatusService
{
    public MentorStatusResult GetStatus(Zone zone, PlayerRuntimeState state)
    {
        if (state.TeacherCharacterId is not null && state.StudentCharacterId is not null)
            // Should be structurally unreachable -- see class remarks. Kept as a loud diagnostic instead
            // of a silent default so a future invariant break surfaces immediately rather than as a
            // mystery wrong-partner report.
            logger.LogError(
                "Mentor status invariant violated: character {CharacterId} has both TeacherCharacterId {TeacherCharacterId} and StudentCharacterId {StudentCharacterId} set -- falling back to the teacher-side check",
                state.CharacterId, state.TeacherCharacterId, state.StudentCharacterId);

        var iAmTheStudent = state.TeacherCharacterId is not null;
        var partnerId = iAmTheStudent ? state.TeacherCharacterId!.Value : state.StudentCharacterId;

        if (partnerId is null)
        {
            // Client-visible as a session disconnect (MentorStatusHandler aborts on this outcome).
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
                .PartnerNotInZone); // partner not in this same zone -- no reply
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
