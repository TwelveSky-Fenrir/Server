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
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <remarks>
///     WS1.4 ASK-PUBLISH-ONLY: <see cref="AskAsync" />'s cross-shard fallback publishes an Ask row via
///     <see cref="ISocialCrossShardRelayQueue" /> and registers the master-side busy gate
///     (<see cref="MentorRegistry.TryAskCrossShard" />), but no <c>ISocialCrossShardRelayHandler</c> is
///     registered for <see cref="SocialCrossShardRelayKind.Mentor" /> -- see <c>DuelService</c>'s own remarks
///     for the shared rationale. <see cref="MentorRegistry.TryCancel" /> still consumes the outbound entry,
///     so a master is never left permanently busy even though the ask itself is never delivered today. A
///     follow-up closing this gap needs a <c>MentorCrossShardRelayHandler</c> mirroring
///     <c>FriendCrossShardRelayHandler</c>, including the target's own level-gate re-check.
/// </remarks>
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

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name. The level/existing-teacher/existing-student disconnect gate already
    ///     ran before target resolution here; the still-negotiating soft-busy check
    ///     (<see cref="MentorRegistry.IsNegotiating" />, <see cref="IsExcludedByCommunityWork" />) did not, and
    ///     is moved up to join it so a busy master naming a nonexistent/offline student gets the busy reply,
    ///     not "target not found". The same <see cref="MentorRegistry.IsNegotiating" /> check inside
    ///     <see cref="MentorRegistry.TryAsk" /> stays in place for the actual registration.
    ///     <para>
    ///         Server/ts25zone/S07_MyGame04.cpp:185-216 (<c>CheckCommunityWork</c>'s shared seven-flag busy
    ///         check) is also re-applied to the resolved target below (<see cref="IsExcludedByCommunityWork" />),
    ///         mirroring <c>GuildInviteService.IsExcludedByCommunityWorkOrStunDeath</c>/
    ///         <c>TradeInviteService.IsExcludedByCommunityWorkOrStunDeath</c> -- this closes a gap where only
    ///         this family's own <see cref="MentorRegistry" /> state was previously consulted for either side.
    ///         The stun/post-death gate those two siblings additionally apply is deliberately NOT included here:
    ///         no contract citation confirms it applies to this opcode pair, so it is left unmodeled rather than
    ///         guessed at.
    ///     </para>
    /// </remarks>
    public async ValueTask<MentorAskResult> AskAsync(Zone zone, PlayerRuntimeState master, string targetAvatarName,
        CancellationToken cancellationToken)
    {
        if (master.Level < MinimumMasterLevel || master.TeacherCharacterId is not null ||
            master.StudentCharacterId is not null)
        {
            // Client-visible as a session disconnect (MentorAskHandler aborts on this outcome) -- a legitimate
            // client never lets an ineligible character open a teacher negotiation.
            logger.LogWarning(
                "Mentor ask rejected: character {MasterId} (level {Level}) is not eligible to be a master -- session will be disconnected",
                master.CharacterId, master.Level);
            return new MentorAskResult(MentorAskResultKind.AskerMustDisconnect);
        }

        if (mentors.IsNegotiating(master.CharacterId) || IsExcludedByCommunityWork(master))
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

        if (student.Tribe != master.Tribe || student.Level >= master.Level)
        {
            // Client-visible as a session disconnect (MentorAskHandler aborts on this outcome).
            logger.LogWarning(
                "Mentor ask rejected: character {MasterId} (level {MasterLevel}, tribe {MasterTribe}) targeted ineligible character {TargetCharacterId} (level {TargetLevel}, tribe {TargetTribe}) -- session will be disconnected",
                master.CharacterId, master.Level, master.Tribe, student.CharacterId, student.Level, student.Tribe);
            return new MentorAskResult(MentorAskResultKind.TargetMustDisconnect);
        }

        if (IsExcludedByCommunityWork(student))
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

    /// <summary>
    ///     WS1.4 same-shard-miss, ASK-PUBLISH-ONLY fallback -- see this class's own remarks. The student's
    ///     level (needed for the level-gate check) is not carried by the cross-shard directory, so that check
    ///     -- along with already-has-teacher/already-has-student -- is deferred to the eventual target-side
    ///     handler; only the same-tribe check (against the directory row's own denormalized Tribe) is
    ///     re-evaluable here.
    /// </summary>
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

    /// <summary>
    ///     <c>CheckCommunityWork()</c>'s six OTHER exclusivity flags (personal shop, duel, trade, friend, party,
    ///     guild negotiation -- this family's own mentor/teacher-negotiation flag is checked separately, and
    ///     atomically, by <see cref="MentorRegistry.IsNegotiating" />/<see cref="MentorRegistry.TryAsk" />). Any
    ///     one of these being true excludes <paramref name="player" /> from starting or receiving a mentor ask,
    ///     exactly as it does for every other ASK family in this codebase (mirrors
    ///     <c>GuildInviteService.IsExcludedByCommunityWorkOrStunDeath</c>/
    ///     <c>TradeInviteService.IsExcludedByCommunityWorkOrStunDeath</c>, minus their additional stun/post-death
    ///     gate -- not modeled here, see this class's own <see cref="Ask" /> remarks).
    /// </summary>
    private bool IsExcludedByCommunityWork(PlayerRuntimeState player)
    {
        return player.PshopOpen
               || duels.IsNegotiating(player.CharacterId)
               || trades.IsBusy(player.CharacterId)
               || friends.IsNegotiating(player.CharacterId)
               || parties.IsNegotiating(player.CharacterId)
               || guildInvites.IsNegotiating(player.CharacterId);
    }
}
