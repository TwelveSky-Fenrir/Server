using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IFriendService" />
public sealed class FriendService(
    ZoneRegistry zones,
    FriendRegistry friends,
    IFriendRepository repository,
    ICharacterShardLocationRepository characterShardLocations)
    : IFriendService
{
    private const int MaxFriends = 10;

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name, so a busy asker naming a nonexistent/offline target gets the busy
    ///     reply, not "target not found". <see cref="FriendRegistry.IsNegotiating" /> is therefore checked
    ///     ahead of <see cref="FindPlayerByName" />; the same check inside <see cref="FriendRegistry.TryAsk" />
    ///     stays in place for the actual registration.
    /// </remarks>
    public FriendAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (zone.MapId == 124)
            return FriendAskResultKind.MapForbidden;

        if (friends.IsNegotiating(asker.CharacterId))
            return FriendAskResultKind.AskerBusy;

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
            return FriendAskResultKind.TargetNotFound;

        if (asker.Friends.Count >= MaxFriends || asker.Friends.Values.Contains(target.CharacterId))
            return FriendAskResultKind.AlreadyFriendOrFull;

        if (asker.Tribe != target.Tribe)
            return FriendAskResultKind.TribeMismatch;

        switch (friends.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case FriendAskOutcome.AskerBusy:
                return FriendAskResultKind.AskerBusy;
            case FriendAskOutcome.TargetBusy:
                return FriendAskResultKind.TargetBusy;
            case FriendAskOutcome.Sent:
                target.Session.Send(new FriendResponse { AvatarName = asker.Name });
                return FriendAskResultKind.Sent;
            default:
                return FriendAskResultKind.AskerBusy;
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!friends.TryAnswer(targetId, answerCode == 0, out var askerId))
            return;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new FriendAnswerResponse { Answer = answerCode });
    }

    public void Cancel(int askerId)
    {
        if (!friends.TryCancel(askerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new FriendCancelResponse());
    }

    public async ValueTask<FriendLocateResult> LocateAsync(PlayerRuntimeState asker, int index,
        CancellationToken cancellationToken)
    {
        if (index is < 0 or >= MaxFriends)
            return new FriendLocateResult(FriendLocateResultKind.IndexOutOfRange);

        if (!asker.Friends.TryGetValue((byte)index, out var friendId))
            return new FriendLocateResult(FriendLocateResultKind.SlotEmpty);

        if (zones.TryGetPlayer(friendId, out var friend))
            return new FriendLocateResult(FriendLocateResultKind.Found,
                friend.Tribe == asker.Tribe ? friend.MapId : -1);

        // Same-shard miss -- fall back to the cross-shard directory, re-applying the same same-tribe gate
        // against the row's own denormalized Tribe column (no second query needed). A deliberate,
        // low-frequency player action (a friend-locate ping), not a per-tick path.
        var remote = await characterShardLocations.FindByCharacterIdAsync(friendId, cancellationToken)
            .ConfigureAwait(false);

        var zoneNumber = remote is { } row && row.Tribe == asker.Tribe ? row.MapId : -1;
        return new FriendLocateResult(FriendLocateResultKind.Found, zoneNumber);
    }

    public async ValueTask<FriendAddResult> AddAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken)
    {
        if (index is < 0 or >= MaxFriends || state.Friends.ContainsKey((byte)index))
            return new FriendAddResult(FriendAddResultKind.InvalidSlot);

        if (!friends.TryConsumeAccepted(state.CharacterId, out var otherId))
            return new FriendAddResult(FriendAddResultKind.NoPendingAccept);

        var slot = (byte)index;
        await repository.AddAsync(state.CharacterId, slot, otherId, cancellationToken);

        state.Friends[slot] = otherId;

        var otherName = zones.TryGetPlayer(otherId, out var other) ? other.Name : "";
        return new FriendAddResult(FriendAddResultKind.Added, otherName);
    }

    public async ValueTask<FriendRemoveResultKind> RemoveAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken)
    {
        if (index is < 0 or >= MaxFriends || !state.Friends.ContainsKey((byte)index))
            return FriendRemoveResultKind.InvalidSlot;

        var slot = (byte)index;
        await repository.RemoveAsync(state.CharacterId, slot, cancellationToken);
        state.Friends.TryRemove(slot, out _);

        return FriendRemoveResultKind.Removed;
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}
