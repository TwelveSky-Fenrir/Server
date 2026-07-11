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

public sealed class FriendService(
    ZoneRegistry zones,
    FriendRegistry friends,
    DuelRegistry duels,
    TradeRegistry trades,
    PartyRegistry parties,
    GuildInviteRegistry guildInvites,
    MentorRegistry mentors,
    IFriendRepository repository,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<FriendService> logger)
    : IFriendService
{
    private const int MaxFriends = 10;

        public async ValueTask<FriendAskResultKind> AskAsync(Zone zone, PlayerRuntimeState asker, string targetAvatarName,
        CancellationToken cancellationToken)
    {
        if (zone.MapId == 124)
        {
            logger.LogDebug("Friend ask ignored: character {AskerId} is on the scripted-duel map (124)",
                asker.CharacterId);
            return FriendAskResultKind.MapForbidden;
        }

        if (asker.Friends.Count >= MaxFriends)
        {
            logger.LogDebug(
                "Friend ask rejected: character {AskerId} already has {FriendCount} friends",
                asker.CharacterId, asker.Friends.Count);
            return FriendAskResultKind.AlreadyFriendOrFull;
        }

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is not null)
        {
            if (asker.Friends.Values.Contains(target.CharacterId))
            {
                logger.LogDebug(
                    "Friend ask rejected: character {AskerId} is already friends with {TargetCharacterId}",
                    asker.CharacterId, target.CharacterId);
                return FriendAskResultKind.AlreadyFriendOrFull;
            }

            if (CommunityWorkGate.IsBusy(asker, duels, trades, friends, parties, mentors, guildInvites))
            {
                logger.LogDebug("Friend ask rejected: character {AskerId} already has a pending negotiation",
                    asker.CharacterId);
                return FriendAskResultKind.AskerBusy;
            }

            if (asker.Tribe != target.Tribe)
            {
                logger.LogWarning(
                    "Friend ask rejected: character {AskerId} (tribe {AskerTribe}) targeted character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                    asker.CharacterId, asker.Tribe, target.CharacterId, target.Tribe);
                return FriendAskResultKind.TribeMismatch;
            }

            if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites))
            {
                logger.LogDebug("Friend ask rejected: target character {TargetCharacterId} is busy",
                    target.CharacterId);
                return FriendAskResultKind.TargetBusy;
            }

            switch (friends.TryAsk(asker.CharacterId, target.CharacterId))
            {
                case FriendAskOutcome.AskerBusy:
                    logger.LogDebug("Friend ask rejected: character {AskerId} is busy", asker.CharacterId);
                    return FriendAskResultKind.AskerBusy;
                case FriendAskOutcome.TargetBusy:
                    logger.LogDebug("Friend ask rejected: target character {TargetCharacterId} is busy",
                        target.CharacterId);
                    return FriendAskResultKind.TargetBusy;
                case FriendAskOutcome.Sent:
                    target.Session.Send(new FriendResponse { AvatarName = asker.Name });
                    logger.LogDebug(
                        "Friend ask sent: character {AskerId} ({AskerName}) -> character {TargetCharacterId} ({TargetName})",
                        asker.CharacterId, asker.Name, target.CharacterId, target.Name);
                    return FriendAskResultKind.Sent;
                default:
                    logger.LogDebug("Friend ask rejected: character {AskerId} is busy (unmatched outcome)",
                        asker.CharacterId);
                    return FriendAskResultKind.AskerBusy;
            }
        }

        if (CommunityWorkGate.IsBusy(asker, duels, trades, friends, parties, mentors, guildInvites))
        {
            logger.LogDebug("Friend ask rejected: character {AskerId} already has a pending negotiation",
                asker.CharacterId);
            return FriendAskResultKind.AskerBusy;
        }

        return await AskCrossShardAsync(asker, targetAvatarName, cancellationToken).ConfigureAwait(false);
    }

        public void Answer(int targetId, int answerCode)
    {
        if (friends.TryConsumeCrossShardInbound(targetId, out var inbound))
        {
            var accepted = answerCode == 0;
            if (accepted)
                friends.MarkAccepted(targetId, inbound.SourceCharacterId);

            var targetName = zones.TryGetPlayer(targetId, out var targetState) ? targetState.Name : "";

            crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                SocialCrossShardRelayKind.Friend,
                SocialCrossShardRelayMessageType.Answer,
                accepted,
                null,
                options.Value.ShardId,
                targetId,
                targetName,
                inbound.SourceShardId,
                inbound.SourceCharacterId,
                inbound.RelayId));

            logger.LogDebug(
                "Friend answer (cross-shard): character {TargetId} answered {AnswerCode} to asker {AskerId} on shard {AskerShardId}",
                targetId, answerCode, inbound.SourceCharacterId, inbound.SourceShardId);
            return;
        }

        if (!friends.TryAnswer(targetId, answerCode == 0, out var askerId))
        {
            logger.LogDebug("Friend answer ignored: character {TargetId} has no pending ask", targetId);
            return;
        }

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new FriendAnswerResponse { Answer = answerCode });

        logger.LogDebug("Friend answer: character {TargetId} answered {AnswerCode} to asker {AskerId}", targetId,
            answerCode, askerId);
    }

    public void Cancel(int askerId)
    {
        if (!friends.TryCancel(askerId, out var targetId))
        {
            logger.LogDebug("Friend cancel ignored: character {AskerId} has no pending ask to cancel", askerId);
            return;
        }

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new FriendCancelResponse());

        logger.LogDebug("Friend ask cancelled: character {AskerId} withdrew ask to character {TargetId}", askerId,
            targetId);
    }

    public async ValueTask<FriendLocateResult> LocateAsync(PlayerRuntimeState asker, int index,
        CancellationToken cancellationToken)
    {
        if (index is < 0 or >= MaxFriends)
        {
            logger.LogDebug("Friend locate rejected: character {AskerId} sent out-of-range slot {Index}",
                asker.CharacterId, index);
            return new FriendLocateResult(FriendLocateResultKind.IndexOutOfRange);
        }

        if (!asker.Friends.TryGetValue((byte)index, out var friendId))
        {
            logger.LogWarning(
                "Friend locate rejected: character {AskerId} slot {Index} is empty -- session will be disconnected",
                asker.CharacterId, index);
            return new FriendLocateResult(FriendLocateResultKind.SlotEmpty);
        }

        if (zones.TryGetPlayer(friendId, out var friend))
        {
            var sameShardZone = friend.Tribe == asker.Tribe ? friend.MapId : -1;
            logger.LogDebug(
                "Friend locate resolved (same shard): character {AskerId} slot {Index} -> friend {FriendId} on map {ZoneNumber}",
                asker.CharacterId, index, friendId, sameShardZone);
            return new FriendLocateResult(FriendLocateResultKind.Found, sameShardZone);
        }

        var remote = await characterShardLocations.FindByCharacterIdAsync(friendId, cancellationToken)
            .ConfigureAwait(false);

        var zoneNumber = remote is { } row && row.Tribe == asker.Tribe ? row.MapId : -1;
        logger.LogDebug(
            "Friend locate resolved (cross-shard directory): character {AskerId} slot {Index} -> friend {FriendId} on map {ZoneNumber}",
            asker.CharacterId, index, friendId, zoneNumber);
        return new FriendLocateResult(FriendLocateResultKind.Found, zoneNumber);
    }

    public async ValueTask<FriendAddResult> AddAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken)
    {
        if (index is < 0 or >= MaxFriends || state.Friends.ContainsKey((byte)index))
        {
            logger.LogWarning(
                "Friend add rejected: character {CharacterId} slot {Index} is out of range or already occupied -- session will be disconnected",
                state.CharacterId, index);
            return new FriendAddResult(FriendAddResultKind.InvalidSlot);
        }

        if (!friends.TryConsumeAccepted(state.CharacterId, out var otherId))
        {
            logger.LogDebug("Friend add ignored: character {CharacterId} has no accepted friend request to consume",
                state.CharacterId);
            return new FriendAddResult(FriendAddResultKind.NoPendingAccept);
        }

        var slot = (byte)index;
        await repository.AddAsync(state.CharacterId, slot, otherId, cancellationToken);

        state.Friends[slot] = otherId;

        var otherName = zones.TryGetPlayer(otherId, out var other) ? other.Name : "";
        logger.LogInformation("Friend added: character {CharacterId} added character {OtherId} to slot {Index}",
            state.CharacterId, otherId, index);
        return new FriendAddResult(FriendAddResultKind.Added, otherName);
    }

    public async ValueTask<FriendRemoveResultKind> RemoveAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken)
    {
        if (index is < 0 or >= MaxFriends)
        {
            logger.LogDebug("Friend remove ignored: character {CharacterId} sent out-of-range slot {Index}",
                state.CharacterId, index);
            return FriendRemoveResultKind.IndexOutOfRange;
        }

        if (!state.Friends.ContainsKey((byte)index))
        {
            logger.LogWarning(
                "Friend remove rejected: character {CharacterId} slot {Index} is already empty -- session will be disconnected",
                state.CharacterId, index);
            return FriendRemoveResultKind.SlotEmpty;
        }

        var slot = (byte)index;
        await repository.RemoveAsync(state.CharacterId, slot, cancellationToken);
        state.Friends.TryRemove(slot, out _);

        logger.LogInformation("Friend removed: character {CharacterId} removed slot {Index}", state.CharacterId,
            index);
        return FriendRemoveResultKind.Removed;
    }

        private async ValueTask<FriendAskResultKind> AskCrossShardAsync(PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Friend ask rejected: character {AskerId} target {TargetAvatarName} not found on any shard",
                asker.CharacterId, targetAvatarName);
            return FriendAskResultKind.TargetNotFound;
        }

        if (asker.Friends.Values.Contains(remote.CharacterId))
        {
            logger.LogDebug(
                "Friend ask rejected: character {AskerId} is already friends with cross-shard character {TargetCharacterId}",
                asker.CharacterId, remote.CharacterId);
            return FriendAskResultKind.AlreadyFriendOrFull;
        }

        if (asker.Tribe != remote.Tribe)
        {
            logger.LogWarning(
                "Friend ask rejected: character {AskerId} (tribe {AskerTribe}) targeted cross-shard character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                asker.CharacterId, asker.Tribe, remote.CharacterId, remote.Tribe);
            return FriendAskResultKind.TribeMismatch;
        }

        var outcome = friends.TryAskCrossShard(asker.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        if (outcome != FriendAskOutcome.Sent)
        {
            logger.LogDebug("Friend ask rejected: character {AskerId} is busy (cross-shard registration)",
                asker.CharacterId);
            return FriendAskResultKind.AskerBusy;
        }

        crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.Friend,
            SocialCrossShardRelayMessageType.Ask,
            null,
            null,
            options.Value.ShardId,
            asker.CharacterId,
            asker.Name,
            remote.ShardId,
            remote.CharacterId,
            null));

        logger.LogDebug(
            "Friend ask published cross-shard: character {AskerId} ({AskerName}) -> character {TargetCharacterId} on shard {TargetShardId}",
            asker.CharacterId, asker.Name, remote.CharacterId, remote.ShardId);
        return FriendAskResultKind.SentCrossShard;
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
