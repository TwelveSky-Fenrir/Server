using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     Level-gap check uses <see cref="PlayerRuntimeState.CombinedLevel" /> (aLevel1+aLevel2) on both sides,
///     per the party-invite level-gate behavior contract.
/// </summary>
public sealed class PartyInviteService(
    PartyRegistry parties,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyInviteService> logger)
    : IPartyInviteService
{
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name, so an already-partied-and-not-leader or still-negotiating inviter
    ///     naming a nonexistent/offline target gets that outcome, not "target not found". These two
    ///     inviter-only checks mirror <see cref="PartyRegistry.TryInvite" />'s own already-verified internal
    ///     ordering (see that method's "Check order verified" remark) and are therefore run ahead of the
    ///     by-name target lookup below; <see cref="PartyRegistry.TryInvite" /> itself still performs the
    ///     full check sequence (including these two again) for the actual registration.
    ///     <para>
    ///         Party-invite level-gate combined-level extension: Server/ts25zone/S04_MyWork02.cpp:9608-9614
    ///         (combined-level computation for both sides, feeding <see cref="PartyRegistry.TryInvite" />'s own
    ///         already-generic <c>inviterCumulativeLevel</c>/<c>inviteeCumulativeLevel</c> parameters) ;
    ///         Server/ts25zone/S04_MyWork02.cpp:9603-9607 (the tribe/alliance disconnect check immediately
    ///         preceding, confirming check order is unaffected by this).
    ///     </para>
    ///     <para>
    ///         WS1.4: on a same-shard by-name miss, falls back to
    ///         <see cref="ICharacterShardLocationRepository.FindByNameAsync" /> before reporting
    ///         <see cref="PartyInviteResultKind.TargetNotFound" /> -- see
    ///         <see cref="PartyInviteResultKind.SentCrossShard" />'s own remarks. The invitee-already-partied
    ///         and level-gap checks are NOT re-run cross-shard (see
    ///         <see cref="PartyRegistry.TryInviteCrossShard" />'s own remarks for why); only the same-tribe
    ///         check (against the resolved row's own denormalized Tribe) is.
    ///     </para>
    /// </remarks>
    public async ValueTask<PartyInviteResult> InviteAsync(Zone zone, PlayerRuntimeState inviter,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        if (parties.IsInParty(inviter.CharacterId) && !parties.IsLeader(inviter.CharacterId))
        {
            // Protocol violation: a legitimate client never lets a non-leader party member open a new invite --
            // the inviter's own session is aborted by the caller, so this is worth surfacing by default.
            logger.LogWarning(
                "Party invite rejected: character {InviterCharacterId} is already partied and not the leader -- session will be disconnected",
                inviter.CharacterId);
            return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
        }

        if (parties.IsNegotiating(inviter.CharacterId))
        {
            logger.LogDebug("Party invite rejected: character {InviterCharacterId} already has a pending invite",
                inviter.CharacterId);
            return new PartyInviteResult(PartyInviteResultKind.InviterBusy);
        }

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
            return await InviteCrossShardAsync(inviter, targetAvatarName, cancellationToken).ConfigureAwait(false);

        var outcome = parties.TryInvite(inviter.CharacterId, inviter.CombinedLevel, inviter.Tribe,
            target.CharacterId, target.CombinedLevel, target.Tribe);

        switch (outcome)
        {
            case PartyInviteOutcome.InviterMustDisconnect:
                logger.LogWarning(
                    "Party invite rejected: character {InviterCharacterId} must disconnect (registry check against target {TargetCharacterId})",
                    inviter.CharacterId, target.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
            case PartyInviteOutcome.InviterBusy:
                logger.LogDebug("Party invite rejected: character {InviterCharacterId} is busy",
                    inviter.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterBusy);
            case PartyInviteOutcome.TargetBusy:
                logger.LogDebug("Party invite rejected: target character {TargetCharacterId} is busy",
                    target.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.TargetBusy);
            case PartyInviteOutcome.TargetAlreadyPartied:
                logger.LogDebug("Party invite rejected: target character {TargetCharacterId} is already partied",
                    target.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.TargetAlreadyPartied);
            default:
                logger.LogDebug(
                    "Party invite sent: character {InviterCharacterId} ({InviterName}) -> character {TargetCharacterId} ({TargetName})",
                    inviter.CharacterId, inviter.Name, target.CharacterId, target.Name);
                return new PartyInviteResult(PartyInviteResultKind.Sent, target.CharacterId, target.Name,
                    inviter.Name);
        }
    }

    /// <summary>
    ///     WS1.4 same-shard-miss fallback -- see <see cref="InviteAsync" />'s own remarks for exactly which
    ///     pre-checks are (and are not) re-evaluated here.
    /// </summary>
    private async ValueTask<PartyInviteResult> InviteCrossShardAsync(PlayerRuntimeState inviter,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Party invite rejected: character {InviterCharacterId} target {TargetAvatarName} not found on any shard",
                inviter.CharacterId, targetAvatarName);
            return new PartyInviteResult(PartyInviteResultKind.TargetNotFound);
        }

        if (inviter.Tribe != remote.Tribe)
        {
            logger.LogWarning(
                "Party invite rejected: character {InviterCharacterId} (tribe {InviterTribe}) targeted cross-shard character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                inviter.CharacterId, inviter.Tribe, remote.CharacterId, remote.Tribe);
            return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
        }

        var outcome = parties.TryInviteCrossShard(inviter.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        switch (outcome)
        {
            case PartyInviteOutcome.InviterMustDisconnect:
                logger.LogWarning(
                    "Party invite rejected: character {InviterCharacterId} must disconnect (registry check, cross-shard)",
                    inviter.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
            case PartyInviteOutcome.Sent:
                crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                    SocialCrossShardRelayKind.Party,
                    SocialCrossShardRelayMessageType.Ask,
                    null,
                    null,
                    options.Value.ShardId,
                    inviter.CharacterId,
                    inviter.Name,
                    remote.ShardId,
                    remote.CharacterId,
                    null));

                logger.LogDebug(
                    "Party invite published cross-shard: character {InviterCharacterId} ({InviterName}) -> character {TargetCharacterId} on shard {TargetShardId}",
                    inviter.CharacterId, inviter.Name, remote.CharacterId, remote.ShardId);
                return new PartyInviteResult(PartyInviteResultKind.SentCrossShard);
            default:
                logger.LogDebug(
                    "Party invite rejected: character {InviterCharacterId} is busy (cross-shard registration)",
                    inviter.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterBusy);
        }
    }
}
