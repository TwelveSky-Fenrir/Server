using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorAskService(
    MentorRegistry mentors,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    GuildInviteRegistry guildInvites,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<MentorAskService> logger) : IMentorAskService
{
    private const int MinimumMasterLevel = 113;

    public async ValueTask<MentorAskResult> AskAsync(Zone zone, PlayerRuntimeState master, string targetAvatarName,
        CancellationToken cancellationToken)
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
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                student = candidate;
                break;
            }

        if (student is null)
            return await AskCrossShardAsync(master, targetAvatarName, cancellationToken).ConfigureAwait(false);

        // Live check is master-relative, not a fixed ceiling: MG5ORIGIN is unconditionally #defined
        // with no #undef anywhere under Server/, so the #else branch is what compiles.
        // S04_MyWork02.cpp:9050-9058; DEFINE.h:18.
        if (student.Tribe != master.Tribe || student.Level >= master.Level)
        {
            logger.LogWarning(
                "Mentor ask rejected: character {MasterId} (level {MasterLevel}, tribe {MasterTribe}) targeted ineligible character {TargetCharacterId} (level {TargetLevel}, tribe {TargetTribe}) -- session will be disconnected",
                master.CharacterId, master.Level, master.Tribe, student.CharacterId, student.Level, student.Tribe);
            return new MentorAskResult(MentorAskResultKind.TargetMustDisconnect);
        }

        if (CommunityWorkGate.IsBusy(student, duels, trades, friends, parties, mentors, guildInvites) ||
            student.IsStunned || student.IsDead)
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

    private async ValueTask<MentorAskResult> AskCrossShardAsync(PlayerRuntimeState master, string targetAvatarName,
        CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Mentor ask rejected: character {MasterId} target {TargetAvatarName} not found on any shard",
                master.CharacterId, targetAvatarName);
            return new MentorAskResult(MentorAskResultKind.TargetNotFound);
        }

        if (remote.Tribe != master.Tribe)
        {
            logger.LogWarning(
                "Mentor ask rejected: character {MasterId} (tribe {MasterTribe}) targeted cross-shard character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                master.CharacterId, master.Tribe, remote.CharacterId, remote.Tribe);
            return new MentorAskResult(MentorAskResultKind.TargetMustDisconnect);
        }

        var outcome = mentors.TryAskCrossShard(master.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        if (outcome != MentorAskOutcome.Sent)
        {
            logger.LogDebug("Mentor ask rejected: character {MasterId} is busy (cross-shard registration)",
                master.CharacterId);
            return new MentorAskResult(MentorAskResultKind.AskerBusy);
        }

        crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.Mentor,
            SocialCrossShardRelayMessageType.Ask,
            null,
            null,
            options.Value.ShardId,
            master.CharacterId,
            master.Name,
            remote.ShardId,
            remote.CharacterId,
            null));

        logger.LogDebug(
            "Mentor ask published cross-shard: character {MasterId} ({MasterName}) -> character {TargetCharacterId} on shard {TargetShardId} (never delivered today -- see MentorAskService's own remarks)",
            master.CharacterId, master.Name, remote.CharacterId, remote.ShardId);
        return new MentorAskResult(MentorAskResultKind.SentCrossShard);
    }
}
