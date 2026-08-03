using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorAskService(
    MentorRegistry mentors,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    GuildInviteRegistry guildInvites,
    ILogger<MentorAskService> logger) : IMentorAskService
{
    private const int MinimumMasterLevel = 113;

    public ValueTask<MentorAskResult> AskAsync(Zone zone, PlayerRuntimeState master, string targetAvatarName,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(Ask(zone, master, targetAvatarName));
    }

    private MentorAskResult Ask(Zone zone, PlayerRuntimeState master, string targetAvatarName)
    {
        if (master.Level < MinimumMasterLevel || master.TeacherCharacterId is not null ||
            master.StudentCharacterId is not null)
        {
            logger.LogWarning(
                "Mentor ask rejected: character {MasterId} (level {Level}) is not eligible to be a master -- session will be disconnected",
                master.CharacterId, master.Level);
            return new MentorAskResult(MentorAskResultKind.AskerMustDisconnect);
        }

        if (CommunityWorkGate.IsBusy(master, duels, trades, friends, parties, mentors, guildInvites) ||
            master.IsStunned || master.IsDead)
        {
            logger.LogDebug("Mentor ask rejected: character {MasterId} already has a pending negotiation",
                master.CharacterId);
            return new MentorAskResult(MentorAskResultKind.AskerBusy);
        }

        PlayerRuntimeState? student = null;
        foreach (var candidate in zone.Players)
            if (candidate.CharacterId != master.CharacterId &&
                string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                student = candidate;
                break;
            }

        if (student is null)
        {
            logger.LogDebug("Mentor ask rejected: character {MasterId} target {TargetAvatarName} is not in the zone",
                master.CharacterId, targetAvatarName);
            return new MentorAskResult(MentorAskResultKind.TargetNotFound);
        }

        if (student.Tribe != master.Tribe || student.Level >= master.Level)
        {
            logger.LogWarning(
                "Mentor ask rejected: character {MasterId} (level {MasterLevel}, tribe {MasterTribe}) targeted ineligible character {TargetCharacterId} (level {TargetLevel}, tribe {TargetTribe}) -- session will be disconnected",
                master.CharacterId, master.Level, master.Tribe, student.CharacterId, student.Level, student.Tribe);
            return new MentorAskResult(MentorAskResultKind.TargetMustDisconnect);
        }

        if (CommunityWorkGate.IsBusy(student, duels, trades, friends, parties, mentors, guildInvites) ||
            student.IsMovingZone || student.IsStunned || student.IsDead)
        {
            logger.LogDebug("Mentor ask rejected: target character {TargetCharacterId} is busy",
                student.CharacterId);
            return new MentorAskResult(MentorAskResultKind.TargetBusy);
        }

        var outcome = mentors.TryAsk(master.CharacterId, student.CharacterId, student.TeacherCharacterId is not null,
            student.StudentCharacterId is not null);

        switch (outcome)
        {
            case MentorAskOutcome.AskerBusy:
                logger.LogDebug("Mentor ask rejected: character {MasterId} is busy", master.CharacterId);
                return new MentorAskResult(MentorAskResultKind.AskerBusy);
            case MentorAskOutcome.TargetBusy:
                logger.LogDebug("Mentor ask rejected: target character {TargetCharacterId} is busy",
                    student.CharacterId);
                return new MentorAskResult(MentorAskResultKind.TargetBusy);
            case MentorAskOutcome.TargetAlreadyHasTeacher:
                logger.LogDebug("Mentor ask rejected: target character {TargetCharacterId} already has a teacher",
                    student.CharacterId);
                return new MentorAskResult(MentorAskResultKind.TargetAlreadyHasTeacher);
            case MentorAskOutcome.TargetAlreadyHasStudent:
                logger.LogDebug("Mentor ask rejected: target character {TargetCharacterId} already has a student",
                    student.CharacterId);
                return new MentorAskResult(MentorAskResultKind.TargetAlreadyHasStudent);
            default:
                logger.LogDebug(
                    "Mentor ask sent: character {MasterId} ({MasterName}) -> character {TargetCharacterId} ({TargetName})",
                    master.CharacterId, master.Name, student.CharacterId, student.Name);
                return new MentorAskResult(MentorAskResultKind.Sent, student.CharacterId, student.Name, master.Name);
        }
    }
}
