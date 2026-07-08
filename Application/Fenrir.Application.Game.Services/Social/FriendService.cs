using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IFriendService" />
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
    ILogger<FriendService> logger)
    : IFriendService
{
    private const int MaxFriends = 10;

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name, so a busy asker naming a nonexistent/offline target gets the busy
    ///     reply, not "target not found". <see cref="FriendRegistry.IsNegotiating" /> and
    ///     <see cref="IsExcludedByCommunityWork" /> are therefore both checked ahead of
    ///     <see cref="FindPlayerByName" />; the same <see cref="FriendRegistry.IsNegotiating" /> check inside
    ///     <see cref="FriendRegistry.TryAsk" /> stays in place for the actual registration.
    ///     <para>
    ///         Server/ts25zone/S07_MyGame04.cpp:185-216 (<c>CheckCommunityWork</c>'s shared seven-flag busy
    ///         check) is also re-applied to the resolved target below (<see cref="IsExcludedByCommunityWork" />),
    ///         mirroring <c>GuildInviteService.IsExcludedByCommunityWorkOrStunDeath</c>/
    ///         <c>TradeInviteService.IsExcludedByCommunityWorkOrStunDeath</c> -- this closes a gap where only
    ///         this family's own <see cref="FriendRegistry" /> state was previously consulted for either side.
    ///         The stun/post-death gate those two siblings additionally apply is deliberately NOT included here:
    ///         no contract citation confirms it applies to this opcode pair, so it is left unmodeled rather than
    ///         guessed at.
    ///     </para>
    /// </remarks>
    public FriendAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (zone.MapId == 124)
        {
            logger.LogDebug("Friend ask ignored: character {AskerId} is on the scripted-duel map (124)",
                asker.CharacterId);
            return FriendAskResultKind.MapForbidden;
        }

        if (friends.IsNegotiating(asker.CharacterId) || IsExcludedByCommunityWork(asker))
        {
            logger.LogDebug("Friend ask rejected: character {AskerId} already has a pending negotiation",
                asker.CharacterId);
            return FriendAskResultKind.AskerBusy;
        }

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
        {
            logger.LogDebug(
                "Friend ask rejected: character {AskerId} target {TargetAvatarName} not found in map {MapId}",
                asker.CharacterId, targetAvatarName, zone.MapId);
            return FriendAskResultKind.TargetNotFound;
        }

        if (asker.Friends.Count >= MaxFriends || asker.Friends.Values.Contains(target.CharacterId))
        {
            logger.LogDebug(
                "Friend ask rejected: character {AskerId} already has {FriendCount} friends or is already friends with {TargetCharacterId}",
                asker.CharacterId, asker.Friends.Count, target.CharacterId);
            return FriendAskResultKind.AlreadyFriendOrFull;
        }

        if (asker.Tribe != target.Tribe)
        {
            // Client-visible as a session disconnect (FriendAskHandler aborts on this outcome) -- a legitimate
            // client never lets a player target a different-tribe avatar for a friend request.
            logger.LogWarning(
                "Friend ask rejected: character {AskerId} (tribe {AskerTribe}) targeted character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                asker.CharacterId, asker.Tribe, target.CharacterId, target.Tribe);
            return FriendAskResultKind.TribeMismatch;
        }

        if (IsExcludedByCommunityWork(target))
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

    public void Answer(int targetId, int answerCode)
    {
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
            // Client-visible as a session disconnect (FriendLocateHandler aborts on this outcome).
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

        // Same-shard miss -- fall back to the cross-shard directory, re-applying the same same-tribe gate
        // against the row's own denormalized Tribe column (no second query needed). A deliberate,
        // low-frequency player action (a friend-locate ping), not a per-tick path.
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
            // Client-visible as a session disconnect (FriendAddHandler aborts on this outcome).
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
            // Client-visible as a session disconnect (FriendRemoveHandler aborts on this outcome).
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

    /// <summary>
    ///     <c>CheckCommunityWork()</c>'s six OTHER exclusivity flags (personal shop, duel, trade, party, guild,
    ///     mentor/teacher negotiation -- this family's own friend-negotiation flag is checked separately, and
    ///     atomically, by <see cref="FriendRegistry.IsNegotiating" />/<see cref="FriendRegistry.TryAsk" />). Any
    ///     one of these being true excludes <paramref name="player" /> from starting or receiving a friend ask,
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
               || parties.IsNegotiating(player.CharacterId)
               || guildInvites.IsNegotiating(player.CharacterId)
               || mentors.IsNegotiating(player.CharacterId);
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
