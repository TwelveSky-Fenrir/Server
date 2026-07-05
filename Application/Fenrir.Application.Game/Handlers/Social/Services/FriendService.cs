using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Social;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of CZ_FRIEND_ASK_SEND's pre-checks, as branched on by <see cref="FriendAskHandler" />.</summary>
public enum FriendAskResultKind
{
    MapForbidden, // map 124 silently ignored (scripted-duel server)
    TargetNotFound,
    AlreadyFriendOrFull,
    TribeMismatch,
    AskerBusy,
    TargetBusy,
    Sent
}

/// <summary>Outcome of CZ_FRIEND_FIND_SEND, as branched on by <see cref="FriendLocateHandler" />.</summary>
public enum FriendLocateResultKind
{
    IndexOutOfRange, // silent no-op, not Abort
    SlotEmpty, // Abort
    Found
}

public readonly record struct FriendLocateResult(FriendLocateResultKind Kind, int ZoneNumber = -1);

/// <summary>Outcome of CZ_FRIEND_MAKE_SEND, as branched on by <see cref="FriendAddHandler" />.</summary>
public enum FriendAddResultKind
{
    InvalidSlot, // Abort
    NoPendingAccept, // silent no-op
    Added
}

public readonly record struct FriendAddResult(FriendAddResultKind Kind, string OtherName = "");

/// <summary>Outcome of CZ_FRIEND_DELETE_SEND, as branched on by <see cref="FriendRemoveHandler" />.</summary>
public enum FriendRemoveResultKind
{
    InvalidSlot, // Abort
    Removed
}

/// <summary>Business logic behind the CZ_FRIEND_* opcode family, extracted from the Friend*Handlers.</summary>
public interface IFriendService
{
    FriendAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName);

    void Answer(int targetId, int answerCode);

    void Cancel(int askerId);

    FriendLocateResult Locate(PlayerRuntimeState asker, int index);

    ValueTask<FriendAddResult> AddAsync(PlayerRuntimeState state, int index, CancellationToken cancellationToken);

    ValueTask<FriendRemoveResultKind> RemoveAsync(PlayerRuntimeState state, int index,
        CancellationToken cancellationToken);
}

/// <inheritdoc cref="IFriendService" />
public sealed class FriendService(ZoneRegistry zones, FriendRegistry friends, IFriendRepository repository)
    : IFriendService
{
    private const int MaxFriends = 10;

    public FriendAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (zone.MapId == 124)
            return FriendAskResultKind.MapForbidden;

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

    public FriendLocateResult Locate(PlayerRuntimeState asker, int index)
    {
        if (index is < 0 or >= MaxFriends)
            return new FriendLocateResult(FriendLocateResultKind.IndexOutOfRange);

        if (!asker.Friends.TryGetValue((byte)index, out var friendId))
            return new FriendLocateResult(FriendLocateResultKind.SlotEmpty);

        var zoneNumber = -1;
        if (zones.TryGetPlayer(friendId, out var friend) && friend.Tribe == asker.Tribe)
            zoneNumber = friend.MapId;

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
