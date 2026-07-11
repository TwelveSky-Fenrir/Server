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
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IGuildInviteService" />
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:9827-9884 (GUILD_ASK_SEND) and
///     Server/ts25zone/S07_MyGame04.cpp:185-216 (<c>CheckCommunityWork</c>'s seven-flag exclusivity check).
///     <see cref="AskAsync" /> composes that check from this process's own sibling negotiation registries
///     (Duel, Trade, Friend, Party, Mentor, plus this family's own <see cref="GuildInviteRegistry" />) and the
///     stun/death action-state gate (<see cref="PlayerRuntimeState.IsStunned" />/<see cref="PlayerRuntimeState.IsDead" />,
///     Server/ts25zone/S07_MyGame04.cpp:438-459,1617-1658) -- both applied to the asker before the target is
///     even resolved, and again to the target once found, matching the legacy check order. The target's own
///     "currently mid zone-transfer" gate (legacy <c>IsMovingZone()</c>) is now modeled too, via
///     <see cref="PlayerRuntimeState.IsMovingZone" /> -- re-verified 2026-07-11 (the flag now exists, see that
///     property's own remarks) and wired into the target-busy check alongside stun/death, matching the pattern
///     already used at <c>Zone.ValleyWar.cs</c>/<c>Zone.Zone038Occupation.cs</c>. Per legacy's own
///     <c>IsMovingZone()</c> semantics this gate is target-only, mirroring <c>UnstunResolver</c>'s cure-attempt
///     check -- it is deliberately NOT added to the asker-busy check above.
///     <para>
///         WS1.4 ASK-PUBLISH-ONLY: <see cref="AskAsync" />'s cross-shard fallback publishes an Ask row via
///         <see cref="ISocialCrossShardRelayQueue" /> and registers the asker-side busy gate
///         (<see cref="GuildInviteRegistry.TryAskCrossShard" />), but no <c>ISocialCrossShardRelayHandler</c>
///         is registered for <see cref="SocialCrossShardRelayKind.GuildInvite" /> -- see <c>DuelService</c>'s
///         own remarks for the shared rationale. <see cref="GuildInviteRegistry.TryCancel" /> still consumes
///         the outbound entry, so an asker is never left permanently busy even though the invite itself is
///         never delivered today. A follow-up closing this gap needs a <c>GuildInviteCrossShardRelayHandler</c>
///         mirroring <c>FriendCrossShardRelayHandler</c>.
///     </para>
/// </remarks>
public sealed class GuildInviteService(
    ZoneRegistry zones,
    GuildInviteRegistry invites,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<GuildInviteService> logger) : IGuildInviteService
{
    public async ValueTask<GuildInviteAskResultKind> AskAsync(Zone zone, PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        if (asker.GuildId is null || !GuildRoleCodec.IsMasterOrSubMaster(asker.GuildRoleDb))
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller is not guild master/sub-master",
                asker.CharacterId);
            return GuildInviteAskResultKind.NotAuthorized;
        }

        if (CommunityWorkGate.IsBusy(asker, duels, trades, friends, parties, mentors, invites) ||
            asker.IsStunned || asker.IsDead)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller busy (community-work/stun/death gate)",
                asker.CharacterId);
            return GuildInviteAskResultKind.AskerBusy;
        }

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
            return await AskCrossShardAsync(asker, targetAvatarName, cancellationToken).ConfigureAwait(false);

        if (target.GuildId is not null)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: target {TargetCharacterId} already guilded",
                asker.CharacterId, target.CharacterId);
            return GuildInviteAskResultKind.TargetAlreadyGuilded;
        }

        if (asker.Tribe != target.Tribe)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: tribe mismatch with target {TargetCharacterId}",
                asker.CharacterId, target.CharacterId);
            return GuildInviteAskResultKind.TribeMismatch;
        }

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, invites) ||
            target.IsStunned || target.IsDead || target.IsMovingZone)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: target {TargetCharacterId} busy",
                asker.CharacterId, target.CharacterId);
            return GuildInviteAskResultKind.TargetBusy;
        }

        switch (invites.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case GuildInviteAskOutcome.AskerBusy:
                logger.LogDebug(
                    "Character {CharacterId} guild invite-ask rejected: caller already has a pending negotiation",
                    asker.CharacterId);
                return GuildInviteAskResultKind.AskerBusy;
            case GuildInviteAskOutcome.TargetBusy:
                logger.LogDebug(
                    "Character {CharacterId} guild invite-ask rejected: target {TargetCharacterId} already has a pending negotiation",
                    asker.CharacterId, target.CharacterId);
                return GuildInviteAskResultKind.TargetBusy;
            case GuildInviteAskOutcome.Sent:
                target.Session.Send(new GuildInviteResponse { AvatarName = asker.Name });
                logger.LogInformation("Character {CharacterId} sent a guild invite to character {TargetCharacterId}",
                    asker.CharacterId, target.CharacterId);
                return GuildInviteAskResultKind.Sent;
            default:
                return GuildInviteAskResultKind.AskerBusy;
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!invites.TryAnswer(targetId, answerCode == 0, out var askerId))
        {
            logger.LogDebug("Character {TargetId} guild invite answer ignored: no pending invite found", targetId);
            return;
        }

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new GuildInviteAnswerResponse { Answer = answerCode });

        logger.LogInformation(
            "Character {TargetId} answered guild invite from character {AskerId}: accepted={Accepted}", targetId,
            askerId, answerCode == 0);
    }

    public void Cancel(int askerId)
    {
        if (!invites.TryCancel(askerId, out var targetId))
        {
            logger.LogDebug("Character {AskerId} guild invite cancel ignored: no pending invite found", askerId);
            return;
        }

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new GuildInviteCancelResponse());

        logger.LogInformation("Character {AskerId} cancelled guild invite to character {TargetId}", askerId,
            targetId);
    }

    /// <summary>
    ///     WS1.4 same-shard-miss, ASK-PUBLISH-ONLY fallback -- see this class's own remarks. The target's
    ///     guild membership (needed for the already-guilded check) is not carried by the cross-shard
    ///     directory, so that check is deferred to the eventual target-side handler; only the same-tribe
    ///     check (against the directory row's own denormalized Tribe) is re-evaluable here.
    /// </summary>
    private async ValueTask<GuildInviteAskResultKind> AskCrossShardAsync(PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask target {TargetName} not found on any shard",
                asker.CharacterId, targetAvatarName);
            return GuildInviteAskResultKind.TargetNotFound;
        }

        if (asker.Tribe != remote.Tribe)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: tribe mismatch with cross-shard target {TargetCharacterId}",
                asker.CharacterId, remote.CharacterId);
            return GuildInviteAskResultKind.TribeMismatch;
        }

        var outcome = invites.TryAskCrossShard(asker.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        if (outcome != GuildInviteAskOutcome.Sent)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller already has a pending negotiation (cross-shard registration)",
                asker.CharacterId);
            return GuildInviteAskResultKind.AskerBusy;
        }

        crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.GuildInvite,
            SocialCrossShardRelayMessageType.Ask,
            null,
            null,
            options.Value.ShardId,
            asker.CharacterId,
            asker.Name,
            remote.ShardId,
            remote.CharacterId,
            null));

        logger.LogInformation(
            "Character {CharacterId} published a guild invite cross-shard to character {TargetCharacterId} on shard {TargetShardId} (never delivered today -- see GuildInviteService's own remarks)",
            asker.CharacterId, remote.CharacterId, remote.ShardId);
        return GuildInviteAskResultKind.SentCrossShard;
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
